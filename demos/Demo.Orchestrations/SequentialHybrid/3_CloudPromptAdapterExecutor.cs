using System.Text.Json;
using Demo.Orchestrations.SequentialHybrid.Models;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Sits AFTER the inbox-picker agent and AFTER the body-redactor agent.
/// Combines their two structured outputs (a <see cref="PickedEmail"/> and a
/// <see cref="RedactedBody"/>) into the single ChatMessage that goes to the
/// cloud writer. Stores both in workflow state for the assembler to read at
/// the end.
///
/// The recipient is never carried in <see cref="PickedEmail"/> — it's always
/// the inbox owner, read from <see cref="InboxService"/> here in the
/// executor and inserted into the cloud-bound prompt and the workflow state.
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
public sealed class CloudPromptAdapterExecutor(InboxService inbox, string id = "cloud-prompt-adapter")
    : ChatProtocolExecutor(id)
{
    public const string SharedScope = "email-triage";
    public const string PickedEmailStateKey = "picked_email";
    public const string RedactionMappingStateKey = "redaction_mapping";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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

        await Log(context, $"received {messages.Count} messages ({assistantTexts.Count} from agents)", cancellationToken);

        var picked = TryDeserialize<PickedEmail>(assistantTexts.FirstOrDefault());
        var redacted = TryDeserialize<RedactedBody>(assistantTexts.LastOrDefault());

        if (picked is null)
        {
            await Log(context, "PickedEmail JSON did not deserialize — workflow will stall", cancellationToken);
            await SendAsync("Inbox picker produced no parseable email. Please try again.", context, cancellationToken);
            return;
        }

        await Log(context, $"stored PickedEmail: from {picked.SenderName} <{picked.SenderEmail}>, subject \"{picked.Subject}\", body {picked.Body.Length} chars", cancellationToken);

        await context.QueueStateUpdateAsync(
            PickedEmailStateKey, picked,
            scopeName: SharedScope,
            cancellationToken: cancellationToken);

        // Deterministic redaction: walk the four typed lists from the
        // on-device spotter, drop anything that isn't literally in the body
        // (defence against hallucinated series), assign tokens here in code
        // (PERSON_1, COMPANY_1, …) and run literal string.Replace.
        var (bodyForCloud, mapping) = ApplyRedactions(picked.Body, redacted);

        if (mapping.Count > 0)
        {
            var preview = string.Join(", ", mapping.Take(4).Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            if (mapping.Count > 4)
                preview += $", … (+{mapping.Count - 4} more)";
            await Log(context, $"stored redaction mapping ({mapping.Count} entries): {preview}", cancellationToken);
            await context.QueueStateUpdateAsync(
                RedactionMappingStateKey, mapping,
                scopeName: SharedScope,
                cancellationToken: cancellationToken);
        }
        else
        {
            await Log(context, "no entities to redact — forwarding raw body", cancellationToken);
        }

        var prompt = $"""
            FROM: {picked.SenderFirstName}
            TO:   {inbox.OwnerFirstName}
            SUBJECT: {picked.Subject}

            {bodyForCloud}

            Please draft just the BODY of a reply email. Do not include a
            greeting (no "Hi X") or a sign-off (no "Best, Y") — the device
            will add those. Keep it concise and professional, 3-6 sentences.
            """;

        await Log(context, $"sending cloud prompt ({prompt.Length} chars, body section {bodyForCloud.Length} chars)", cancellationToken);

        await SendAsync(prompt, context, cancellationToken);
    }

    /// <summary>
    /// Walks the four typed lists from the spotter, drops anything that
    /// doesn't literally appear in the original body (Apple Intelligence
    /// occasionally hallucinates values), assigns a stable token per unique
    /// value (PERSON_1, PERSON_2, COMPANY_1, …) and runs literal
    /// string.Replace over the body. Returns the redacted body and the
    /// token → original map.
    /// </summary>
    private static (string Body, Dictionary<string, string> Mapping) ApplyRedactions(
        string originalBody, RedactedBody? redacted)
    {
        if (redacted is null)
            return (originalBody, []);

        Dictionary<string, string> assigned = new(StringComparer.Ordinal);
        Dictionary<string, string> mapping = new(StringComparer.Ordinal);
        var counters = new int[4]; // PERSON, COMPANY, PROJECT, AMOUNT

        Assign("PERSON",  redacted.PersonLastNames, ref counters[0]);
        Assign("COMPANY", redacted.Companies,       ref counters[1]);
        Assign("PROJECT", redacted.Projects,        ref counters[2]);
        Assign("AMOUNT",  redacted.Amounts,         ref counters[3]);

        // Substitute longest values first so "Project Atlas Q3" isn't half-
        // eaten by "Q3".
        var body = originalBody;
        foreach (var (value, token) in assigned.OrderByDescending(kv => kv.Key.Length))
            body = body.Replace(value, token);

        return (body, mapping);

        void Assign(string kind, IReadOnlyList<string>? values, ref int counter)
        {
            if (values is null)
                return;
            foreach (var raw in values)
            {
                var value = raw?.Trim();
                if (string.IsNullOrEmpty(value))
                    continue;
                // Drop hallucinations that don't literally appear in the body.
                if (!originalBody.Contains(value, StringComparison.Ordinal))
                    continue;
                if (assigned.ContainsKey(value))
                    continue;

                var token = $"{kind}_{++counter}";
                assigned[value] = token;
                mapping[token] = value;
            }
        }
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"cloud-prompt-adapter: {message}"),
            cancellationToken);

    private static ValueTask SendAsync(
        string text, IWorkflowContext context, CancellationToken cancellationToken)
        => context.SendMessageAsync(
            new ChatMessage(ChatRole.User, text),
            cancellationToken: cancellationToken);

    private static T? TryDeserialize<T>(string? text) where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try { return JsonSerializer.Deserialize<T>(text, s_jsonOptions); }
        catch (JsonException) { return null; }
    }
}
