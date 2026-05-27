using System.Text;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Models;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 6 of the meeting-invite pipeline. Pure executor — no LLM.
///
/// Takes the cloud's response body and wraps it in the full email
/// envelope (subject / from / to / mailto link) using fields the cloud
/// never saw:
///
///   • The picked email's <see cref="PickedEmail.Subject"/> and
///     <see cref="PickedEmail.SenderEmail"/> are read from workflow
///     state — written there earlier by
///     <see cref="LocalPickerToStateExecutor"/>. The cloud has never
///     seen these so the privacy story holds even though they appear in
///     the final UI bubble (which renders on-device only).
///   • The owner identity comes from <see cref="InboxService"/>.
///
/// The output renders in the DevUI's MarkdownLabel with a YAML
/// frontmatter block (monospaced metadata header) plus the body and a
/// clickable mailto link.
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
        var cloudBody = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault()
            ?? "_(no invite body generated)_";

        var picked = await context.ReadStateAsync<PickedEmail>(
            LocalPickerToStateExecutor.PickedEmailStateKey,
            scopeName: LocalPickerToStateExecutor.PickedEmailStateScope,
            cancellationToken: cancellationToken);

        var subject = string.IsNullOrWhiteSpace(picked?.Subject)
            ? "Meeting invite"
            : $"Re: {picked!.Subject}";
        var recipientName = picked?.SenderName ?? "(unknown recipient)";
        var recipientEmail = picked?.SenderEmail ?? string.Empty;

        await Log(context,
            $"assembling invite for {recipientName} <{recipientEmail}> (subject: {subject}); cloud body {cloudBody.Length} chars",
            cancellationToken);

        var mailto = BuildMailto(to: recipientEmail, subject: subject, body: cloudBody);

        var markdown = new StringBuilder()
            .AppendLine("---")
            .AppendLine($"from:    \"{inbox.OwnerName}\" <{inbox.OwnerEmail}>")
            .AppendLine($"to:      \"{recipientName}\" <{recipientEmail}>")
            .AppendLine($"subject: {subject}")
            .AppendLine("kind:    meeting invite (draft)")
            .AppendLine("---")
            .AppendLine()
            .AppendLine(cloudBody)
            .ToString();

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
