using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations.SequentialHybrid.Executors;

/// <summary>
/// Terminal executor that yields the final accumulated chat messages as the
/// workflow output. Public counterpart to MAF's internal
/// <c>OutputMessagesExecutor</c>.
///
/// Inherits <see cref="ChatProtocolExecutor"/> so it batches upstream
/// <c>List&lt;ChatMessage&gt;</c> sends across a turn and only yields once
/// per <see cref="TurnToken"/>.
/// </summary>
public sealed class OutputMessagesExecutor(string id = "output-messages")
    : ChatProtocolExecutor(id)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).YieldsOutput<List<ChatMessage>>();

    protected override ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
        => context.YieldOutputAsync(messages, cancellationToken);
}
