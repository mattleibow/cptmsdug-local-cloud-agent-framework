using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Adapter executors that sit BETWEEN agent stages in the email-triage
// workflow. They consume the previous agent's structured output, mutate
// workflow state, and re-emit a clean ChatMessage + TurnToken so the next
// agent can run.
//
// All adapters subclass ChatProtocolExecutor so they accumulate upstream
// List<ChatMessage> sends across a single turn and only fire TakeTurnAsync
// once per TurnToken — same pattern MAF's own AIAgentHostExecutor uses.
// Without this, an upstream agent with ForwardIncomingMessages = true would
// trigger the adapter multiple times.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Terminal executor that yields the final accumulated chat messages as the
/// workflow output. Public counterpart to MAF's internal
/// <c>OutputMessagesExecutor</c>.
/// </summary>
public sealed class OutputMessagesExecutor : ChatProtocolExecutor
{
    public OutputMessagesExecutor(string id = "output-messages") : base(id) { }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).YieldsOutput<List<ChatMessage>>();

    protected override ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
        => context.YieldOutputAsync(messages, cancellationToken);
}

/// <summary>
/// Sits AFTER the inbox-picker agent and AFTER the body-redactor agent.
/// Combines their two structured outputs (a <see cref="PickedEmail"/> and a
/// <see cref="RedactedBody"/>) into the single ChatMessage that goes to the
/// cloud writer. Stores both in workflow state for the assembler to read
/// at the end.
///
/// Cloud-bound prompt looks like:
///
///   From: Bob
///   To: Alex
///   Subject: Q3 budget approval — Project Atlas
///
///   [redacted body — last names / company names replaced with COMPANY_n /
///   PERSON_n / etc., no email addresses]
///
///   Please draft a reply body. No greeting, no sign-off.
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

/// <summary>
/// Sits AFTER the cloud reply-writer and AFTER the summary agent. Builds
/// the final user-facing markdown email by combining:
///   - the picked email stored in workflow state (for the to/from frontmatter),
///   - the user identity from <see cref="UserProfile"/> (for the from line + signature),
///   - the cloud's reply body,
///   - the local summary,
///   - a mailto: link so the user can open the reply in Mail.app.
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
public sealed class FinalEmailAssembler : ChatProtocolExecutor
{
    public FinalEmailAssembler(string id = "final-email-assembler") : base(id) { }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        var picked = await context
            .ReadStateAsync<PickedEmail>(
                CloudPromptAdapter.PickedEmailStateKey,
                scopeName: CloudPromptAdapter.SharedScope,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var mapping = await context
            .ReadStateAsync<Dictionary<string, string>>(
                CloudPromptAdapter.RedactionMappingStateKey,
                scopeName: CloudPromptAdapter.SharedScope,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        await Log(context, $"read state: picked={(picked is null ? "null" : picked.FromFullName)}, mapping entries={mapping.Count}", cancellationToken)
            .ConfigureAwait(false);

        if (picked is null)
        {
            await context.SendMessageAsync(
                new ChatMessage(ChatRole.Assistant, "(No picked email — workflow stalled.)"),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var rawReplyBody = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault()
            ?? "(no reply body generated)";

        // Deterministic rehydration of any tokens the cloud preserved in the
        // reply body. Replace longest keys first so PROJECT_10 isn't half-
        // eaten by PROJECT_1.
        var replyBody = rawReplyBody;
        var tokensReplaced = 0;
        foreach (var (token, original) in mapping.OrderByDescending(kv => kv.Key.Length))
        {
            var before = replyBody;
            replyBody = replyBody.Replace(token, original);
            if (!ReferenceEquals(before, replyBody)) tokensReplaced++;
        }
        await Log(context, $"rehydrated {tokensReplaced} of {mapping.Count} token kinds in cloud reply", cancellationToken)
            .ConfigureAwait(false);

        var subject = picked.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? picked.Subject
            : "Re: " + picked.Subject;

        var fullBody = $"""
            Hi {picked.FromFirstName},

            {replyBody}

            Best,
            {UserProfile.Name.Split(' ').First()}
            """;

        var mailto = BuildMailto(picked.FromEmail, subject, fullBody);

        var markdown = new StringBuilder()
            .AppendLine("---")
            .AppendLine($"to: \"{picked.FromFullName}\" <{picked.FromEmail}>")
            .AppendLine($"from: \"{UserProfile.Name}\" <{UserProfile.Email}>")
            .AppendLine($"subject: {subject}")
            .AppendLine("---")
            .AppendLine()
            .AppendLine(fullBody)
            .AppendLine()
            .AppendLine($"[Open in Mail]({mailto})")
            .ToString();

        await Log(context, $"assembled markdown ({markdown.Length} chars)", cancellationToken)
            .ConfigureAwait(false);

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.Assistant, markdown),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"final-email-assembler: {message}"),
            cancellationToken);

    private static string BuildMailto(string toEmail, string subject, string body) =>
        $"mailto:{toEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
}
