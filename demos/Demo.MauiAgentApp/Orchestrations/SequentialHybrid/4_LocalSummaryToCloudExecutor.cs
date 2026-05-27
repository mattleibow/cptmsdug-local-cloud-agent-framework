using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 4 of the meeting-invite pipeline. Pure executor — no LLM.
///
/// One-way valve between the on-device side of the pipeline and the cloud
/// drafter. Takes the last assistant message produced upstream (the
/// summariser's brief) and forwards a single fresh user-role message
/// containing only that text. Nothing else from the prior turns crosses
/// the boundary:
///
///   • No raw email body
///   • No JSON wrappers
///   • No original user prompt
///   • No agent system prompts
///
/// Paired with <c>ForwardIncomingMessages = false</c> on the cloud
/// drafter agent so the drafter sees ONLY what we forward here.
/// </summary>
public sealed class LocalSummaryToCloudExecutor(string id = "local-summary-to-cloud")
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
        var summary = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(summary))
        {
            await Log(context, "no summariser output found — emitting empty user message", cancellationToken);
            summary = "(no summary)";
        }
        else
        {
            await Log(context,
                $"forwarding summary ({summary.Length} chars) to cloud; dropping {messages.Count - 1} upstream message(s)",
                cancellationToken);
        }

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, summary),
            cancellationToken: cancellationToken);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"local-summary-to-cloud: {message}"),
            cancellationToken);
}
