using System.Text;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 5 of the meeting-invite pipeline. Pure executor — no LLM.
///
/// Takes the cloud's Markdown invite draft and adds an on-device "envelope"
/// the user can actually act on:
///
///   • YAML frontmatter showing the inbox owner as the sender. The
///     frontmatter renders as a monospaced metadata header in the DevUI's
///     MarkdownLabel.
///   • A clickable mailto: link the user can tap to open the invite in
///     their email client with the body pre-filled.
///
/// We never reach back into the original picked email here — everything
/// the user sees was either (a) authored by the cloud from the redacted
/// brief or (b) added on-device from <see cref="InboxService"/>. The
/// recipient name in the To: header comes from the cloud's draft.
/// </summary>
public sealed class LocalInviteFinaliserExecutor(InboxService inbox, string id = "local-invite-finaliser")
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
        var cloudDraft = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault()
            ?? "_(no invite draft generated)_";

        await Log(context, $"cloud draft length: {cloudDraft.Length} chars", cancellationToken);

        var mailto = BuildMailto(
            to: string.Empty, // customer email is not in scope at this stage
            subject: "Meeting invite",
            body: cloudDraft);

        var markdown = new StringBuilder()
            .AppendLine("---")
            .AppendLine($"from: \"{inbox.OwnerName}\" <{inbox.OwnerEmail}>")
            .AppendLine($"kind: meeting invite (draft)")
            .AppendLine("---")
            .AppendLine()
            .AppendLine(cloudDraft)
            .AppendLine()
            .AppendLine($"[:mail: Open in Mail]({mailto})")
            .ToString();

        await Log(context, $"assembled invite ({markdown.Length} chars)", cancellationToken);

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.Assistant, markdown),
            cancellationToken: cancellationToken);
    }

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"local-invite-finaliser: {message}"),
            cancellationToken);

    private static string BuildMailto(string to, string subject, string body) =>
        $"mailto:{to}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
}
