using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Privacy boundary between the on-device side of the workflow and the
/// cloud-side drafter agent.
///
/// The local stages above (inbox search + summariser) pass their full
/// conversation history forward — that is the default <see cref="AIAgent"/>
/// host behaviour and is what makes multi-turn agent chains work. But in
/// this hybrid demo the next stage runs in the CLOUD, and the history
/// behind it carries the raw picked email (with addresses, phone numbers,
/// passwords, card numbers etc.).
///
/// This executor sits inline between the summariser and the cloud agent
/// and acts as a one-way valve:
///
///   • It accepts the full <c>List&lt;ChatMessage&gt;</c> coming from
///     upstream.
///   • It picks the LAST assistant message (the summariser's brief).
///   • It emits a single fresh user-role message containing only that
///     brief downstream.
///
/// Nothing else from the prior turns crosses the boundary — not the
/// original user prompt, not the raw inbox JSON, not any system prompts.
/// </summary>
public sealed class LocalToCloudPrivacyGateExecutor(string id = "local-to-cloud-gate")
    : ChatProtocolExecutor(id)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        var brief = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(brief))
        {
            await Log(context, "no assistant brief found upstream — emitting empty user message", cancellationToken);
            brief = "(no brief)";
        }
        else
        {
            await Log(context, $"forwarding redacted brief ({brief.Length} chars); dropping {messages.Count - 1} upstream message(s)", cancellationToken);
        }

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, brief),
            cancellationToken: cancellationToken);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"local-to-cloud-gate: {message}"),
            cancellationToken);
}
