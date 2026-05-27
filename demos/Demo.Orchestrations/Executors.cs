using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Small adapter executors that sit BETWEEN agent stages in a sequential
// workflow. They consume the previous agent's structured output, mutate
// workflow state, and re-emit a clean ChatMessage + TurnToken so the next
// agent can run.
//
// This is the pattern shown in the official Microsoft sample
// "MixedWorkflowWithAgentsAndExecutors" — keep agents plain (so streaming,
// chat bubbles, and traces work) and put any cross-stage logic into tiny
// executors instead of customising the agent host.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Structured output for the redactor agent. The redactor is configured with
/// <c>ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema&lt;RedactionResult&gt;()</c>
/// so its reply is always a valid JSON document matching this shape.
/// </summary>
public sealed record RedactionResult(
    string Redacted,
    Dictionary<string, string> Tokens);

/// <summary>
/// Terminal executor that yields the final accumulated chat messages as the
/// workflow output. Mirrors MAF's internal <c>OutputMessagesExecutor</c>
/// (which is not public) but built on the public <see cref="ChatProtocolExecutor"/>
/// base so it cleanly accepts both <c>List&lt;ChatMessage&gt;</c> sends and
/// the <see cref="TurnToken"/> from upstream <see cref="AIAgent.BindAsExecutor"/>
/// stages.
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
/// Adapter that sits AFTER the redactor agent. It:
/// <list type="number">
///   <item>Reads the redactor's JSON-formatted reply (<see cref="RedactionResult"/>).</item>
///   <item>Stores the <c>Tokens</c> map in workflow state so a later stage
///         can rehydrate the cloud reply.</item>
///   <item>Forwards a clean <see cref="ChatMessage"/> containing just the
///         redacted text, followed by a <see cref="TurnToken"/>, so the next
///         agent stage runs against tokenised content only.</item>
/// </list>
///
/// Inherits <see cref="ChatProtocolExecutor"/> so it accumulates upstream
/// <c>List&lt;ChatMessage&gt;</c> sends across a single turn and only fires
/// <see cref="TakeTurnAsync"/> once per <see cref="TurnToken"/>. This is what
/// MAF's own <c>AIAgentHostExecutor</c> does — it stops adapters from
/// running multiple times when an upstream agent forwards both incoming
/// messages and its response.
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
public sealed class StoreRedactionMapAdapter : ChatProtocolExecutor
{
    public const string RedactionMapStateKey = "redaction_map";

    public StoreRedactionMapAdapter(string id = "store-redaction-map") : base(id) { }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        // The last message in the accumulated turn is the redactor's JSON reply.
        var jsonText = messages
            .LastOrDefault(m => m.Role == ChatRole.Assistant)
            ?.Text
            ?? messages.LastOrDefault()?.Text
            ?? "{}";

        Dictionary<string, string> tokens;
        string redactedText;
        try
        {
            var parsed = JsonSerializer.Deserialize<RedactionResult>(jsonText)
                ?? new RedactionResult(jsonText, []);
            tokens = parsed.Tokens ?? [];
            redactedText = parsed.Redacted;
        }
        catch (JsonException)
        {
            tokens = [];
            redactedText = jsonText;
        }

        await context.QueueStateUpdateAsync(RedactionMapStateKey, tokens, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Send only the tokenised text downstream. Base class auto-sends the
        // TurnToken when this method returns (AutoSendTurnToken = true).
        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, redactedText),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Adapter that sits AFTER the cloud reply writer. It:
/// <list type="number">
///   <item>Reads the redaction map back from workflow state.</item>
///   <item>Replaces every <c>PERSON_n / EMAIL_n / ORG_n / PROJECT_n</c>
///         token in the cloud's draft with the original value.</item>
///   <item>Forwards the rehydrated reply as a clean <see cref="ChatMessage"/>
///         plus a <see cref="TurnToken"/> so the next agent can polish it.</item>
/// </list>
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
public sealed class RehydrateAdapter : ChatProtocolExecutor
{
    public RehydrateAdapter(string id = "rehydrate") : base(id) { }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        var cloudReply = messages
            .LastOrDefault(m => m.Role == ChatRole.Assistant)
            ?.Text
            ?? messages.LastOrDefault()?.Text
            ?? string.Empty;

        var tokens = await context
            .ReadStateAsync<Dictionary<string, string>>(
                StoreRedactionMapAdapter.RedactionMapStateKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        // Replace longest tokens first so PROJECT_10 isn't half-eaten by PROJECT_1.
        var rehydrated = cloudReply;
        foreach (var (token, original) in tokens.OrderByDescending(kv => kv.Key.Length))
        {
            rehydrated = rehydrated.Replace(token, original);
        }

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, rehydrated),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
