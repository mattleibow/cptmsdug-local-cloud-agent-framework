using System.Text;
using Demo.Orchestrations.SequentialHybrid.Models;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Sits AFTER the cloud reply-writer. Builds the final user-facing markdown
/// email by combining:
///   - the picked email stored in workflow state (for the sender and subject),
///   - the inbox owner identity from <see cref="InboxService"/> (for the
///     reply's "from" line + signature),
///   - the redaction mapping stored in workflow state (for token rehydration),
///   - the cloud's reply body,
///   - a mailto: link so the user can open the reply in Mail.app.
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
public sealed class FinalEmailAssemblerExecutor(InboxService inbox, string id = "final-email-assembler")
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
        var picked = await context.ReadStateAsync<PickedEmail>(
            CloudPromptAdapterExecutor.PickedEmailStateKey,
            scopeName: CloudPromptAdapterExecutor.SharedScope,
            cancellationToken: cancellationToken);

        var mapping = await context.ReadStateAsync<Dictionary<string, string>>(
            CloudPromptAdapterExecutor.RedactionMappingStateKey,
            scopeName: CloudPromptAdapterExecutor.SharedScope,
            cancellationToken: cancellationToken)
            ?? [];

        await Log(context, $"read state: picked={picked?.SenderName ?? "null"}, mapping entries={mapping.Count}", cancellationToken);

        if (picked is null)
        {
            await context.SendMessageAsync(
                new ChatMessage(ChatRole.Assistant, "(No picked email — workflow stalled.)"),
                cancellationToken: cancellationToken);
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
            if (!ReferenceEquals(before, replyBody))
                tokensReplaced++;
        }
        await Log(context, $"rehydrated {tokensReplaced} of {mapping.Count} token kinds in cloud reply", cancellationToken);

        var subject = picked.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? picked.Subject
            : $"Re: {picked.Subject}";

        var fullBody = $"""
            Hi {picked.SenderFirstName},

            {replyBody}

            Best,
            {inbox.OwnerFirstName}
            """;

        var mailto = BuildMailto(picked.SenderEmail, subject, fullBody);

        var markdown = new StringBuilder()
            .AppendLine("---")
            .AppendLine($"to: \"{picked.SenderName}\" <{picked.SenderEmail}>")
            .AppendLine($"from: \"{inbox.OwnerName}\" <{inbox.OwnerEmail}>")
            .AppendLine($"subject: {subject}")
            .AppendLine("---")
            .AppendLine()
            .AppendLine(fullBody)
            .AppendLine()
            .AppendLine($"[Open in Mail]({mailto})")
            .ToString();

        await Log(context, $"assembled markdown ({markdown.Length} chars)", cancellationToken);

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.Assistant, markdown),
            cancellationToken: cancellationToken);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"final-email-assembler: {message}"),
            cancellationToken);

    private static string BuildMailto(string toEmail, string subject, string body) =>
        $"mailto:{toEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
}

