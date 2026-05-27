using System.Text.Json;
using Demo.Orchestrations.SequentialHybrid.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations.SequentialHybrid.Executors;

/// <summary>
/// Sits AFTER the inbox-picker agent and AFTER the body-redactor agent.
/// Combines their two structured outputs (a <see cref="PickedEmail"/> and a
/// <see cref="RedactedBody"/>) into the single ChatMessage that goes to the
/// cloud writer. Stores both in workflow state for the assembler to read at
/// the end.
///
/// Cloud-bound prompt looks like:
///
///   FROM: Bob
///   TO:   Alex
///   SUBJECT: Q3 budget approval — Project Atlas
///
///   [redacted body with last names / company names / project names /
///   dollar amounts replaced by PERSON_n / COMPANY_n / PROJECT_n / AMOUNT_n]
///
///   Please draft just the BODY of a reply email...
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
public sealed class CloudPromptAdapter : ChatProtocolExecutor
{
    public const string SharedScope = "email-triage";
    public const string PickedEmailStateKey = "picked_email";
    public const string RedactionMappingStateKey = "redaction_mapping";

    public CloudPromptAdapter(string id = "cloud-prompt-adapter") : base(id) { }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        var assistantTexts = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!)
            .ToList();

        await Log(context, $"received {messages.Count} messages ({assistantTexts.Count} from agents)", cancellationToken)
            .ConfigureAwait(false);

        var picked = TryDeserialize<PickedEmail>(assistantTexts.FirstOrDefault());
        var redacted = TryDeserialize<RedactedBody>(assistantTexts.LastOrDefault());

        if (picked is null)
        {
            await Log(context, "PickedEmail JSON did not deserialize — workflow will stall", cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(
                "Inbox picker produced no parseable email. Please try again.",
                context, cancellationToken).ConfigureAwait(false);
            return;
        }

        await Log(context, $"stored PickedEmail: from {picked.FromFullName} <{picked.FromEmail}>, subject \"{picked.Subject}\", body {picked.Body.Length} chars", cancellationToken)
            .ConfigureAwait(false);

        await context.QueueStateUpdateAsync(
            PickedEmailStateKey, picked,
            scopeName: SharedScope,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Deterministic redaction: take the entity list the on-device spotter
        // returned and substitute each into the body. Generates the token
        // names (PERSON_1, COMPANY_1, ...) here in code so the AI never has
        // to invent them.
        var (bodyForCloud, mappingDict) = ApplyRedactions(picked.Body, redacted?.Entities);

        if (mappingDict.Count > 0)
        {
            var mapPreview = string.Join(", ", mappingDict.Take(4).Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            if (mappingDict.Count > 4) mapPreview += $", … (+{mappingDict.Count - 4} more)";
            await Log(context, $"stored redaction mapping ({mappingDict.Count} entries): {mapPreview}", cancellationToken)
                .ConfigureAwait(false);
            await context.QueueStateUpdateAsync(
                RedactionMappingStateKey, mappingDict,
                scopeName: SharedScope,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Log(context, "no entities to redact — forwarding raw body", cancellationToken)
                .ConfigureAwait(false);
        }

        var prompt = $"""
            FROM: {picked.FromFirstName}
            TO:   {picked.ToFirstName}
            SUBJECT: {picked.Subject}

            {bodyForCloud}

            Please draft just the BODY of a reply email. Do not include a
            greeting (no "Hi X") or a sign-off (no "Best, Y") — the device
            will add those. Keep it concise and professional, 3-6 sentences.
            """;

        await Log(context, $"sending cloud prompt ({prompt.Length} chars, body section {bodyForCloud.Length} chars)", cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(prompt, context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Walks the entity list, assigns a stable token to each unique value
    /// (PERSON_1, PERSON_2, COMPANY_1, …) and runs literal string.Replace
    /// over the body. Returns (redacted-body, token → original map).
    /// </summary>
    private static (string Body, Dictionary<string, string> Mapping) ApplyRedactions(
        string originalBody, IReadOnlyList<IdentifiedEntity>? entities)
    {
        if (entities is null || entities.Count == 0)
            return (originalBody, []);

        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            var kind = entity.Kind?.Trim().ToUpperInvariant();
            var value = entity.Value?.Trim();
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(value))
                continue;
            if (kind is not ("PERSON" or "COMPANY" or "PROJECT" or "AMOUNT"))
                continue;
            if (assigned.ContainsKey(value))
                continue;

            counters.TryGetValue(kind, out var n);
            n++;
            counters[kind] = n;

            var token = $"{kind}_{n}";
            assigned[value] = token;
            mapping[token] = value;
        }

        // Substitute longest values first so "Project Atlas Q3" isn't half-
        // eaten by "Q3".
        var body = originalBody;
        foreach (var (value, token) in assigned.OrderByDescending(kv => kv.Key.Length))
            body = body.Replace(value, token);

        return (body, mapping);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"cloud-prompt-adapter: {message}"),
            cancellationToken);

    private static async ValueTask SendAsync(
        string text, IWorkflowContext context, CancellationToken cancellationToken)
        => await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, text), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private static T? TryDeserialize<T>(string? text) where T : class
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonSerializer.Deserialize<T>(text, s_jsonOptions); }
        catch (JsonException) { return null; }
    }
}
