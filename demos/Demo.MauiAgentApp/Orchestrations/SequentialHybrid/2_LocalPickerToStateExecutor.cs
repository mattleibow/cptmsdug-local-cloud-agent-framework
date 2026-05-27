using System.Text;
using System.Text.Json;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Models;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 2 of the meeting-invite pipeline. Pure executor — no LLM.
///
/// Bridges the typed picker output to the rest of the workflow:
///
///   • Reads the picker agent's last assistant message (a JSON
///     <see cref="PickedEmail"/>).
///   • Parses it and stores the whole <see cref="PickedEmail"/> in
///     workflow state under the key <see cref="PickedEmailStateKey"/> so
///     downstream executors that need typed fields (subject, sender
///     email) can read it back without going through the LLM.
///   • Forwards a SINGLE fresh user-role message containing the email
///     formatted as a readable envelope (From / To / Subject / body) so
///     the summariser has the context it needs without seeing the raw
///     JSON wrapper, the original user prompt, or any agent system
///     prompts.
/// </summary>
public sealed class LocalPickerToStateExecutor(InboxService inbox, string id = "local-picker-to-state")
    : ChatProtocolExecutor(id)
{
    /// <summary>
    /// Workflow-state key under which the parsed <see cref="PickedEmail"/>
    /// is stored. Read it back via
    /// <c>context.ReadStateAsync&lt;PickedEmail&gt;(PickedEmailStateKey, PickedEmailStateScope)</c>.
    /// </summary>
    public const string PickedEmailStateKey = "picked-email";

    /// <summary>
    /// Explicit scope name shared by writer (this executor) and reader
    /// (<c>LocalInviteFinaliserExecutor</c>). Without an explicit shared
    /// scope each executor reads/writes its OWN default scope, so the
    /// reader sees null even though the writer succeeded.
    /// </summary>
    public const string PickedEmailStateScope = "shared";

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
        => base.ConfigureProtocol(protocolBuilder).SendsMessage<ChatMessage>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        var lastAssistant = messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!.Trim())
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastAssistant))
        {
            await Log(context, "no picker output found — emitting empty envelope", cancellationToken);
            await context.SendMessageAsync(
                new ChatMessage(ChatRole.User, "(no email picked)"),
                cancellationToken: cancellationToken);
            return;
        }

        PickedEmail? picked = null;
        try
        {
            picked = JsonSerializer.Deserialize<PickedEmail>(
                lastAssistant,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            await Log(context, $"picker output was not valid JSON: {ex.Message}", cancellationToken);
        }

        if (picked is null)
        {
            // Forward whatever the picker gave us as a body so the user
            // still sees something in the UI rather than a hang.
            await context.SendMessageAsync(
                new ChatMessage(ChatRole.User, lastAssistant),
                cancellationToken: cancellationToken);
            return;
        }

        await context.QueueStateUpdateAsync(
            PickedEmailStateKey,
            picked,
            scopeName: PickedEmailStateScope,
            cancellationToken: cancellationToken);

        var envelope = FormatEnvelope(picked, inbox);
        await Log(context,
            $"saved picked email from {picked.SenderName} <{picked.SenderEmail}> to workflow state; forwarding envelope ({envelope.Length} chars)",
            cancellationToken);

        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, envelope),
            cancellationToken: cancellationToken);
    }

    private static string FormatEnvelope(PickedEmail picked, InboxService inbox) =>
        new StringBuilder()
            .AppendLine($"From:    \"{picked.SenderName}\" <{picked.SenderEmail}>")
            .AppendLine($"To:      \"{inbox.OwnerName}\" <{inbox.OwnerEmail}>")
            .AppendLine($"Subject: {picked.Subject}")
            .AppendLine()
            .Append(picked.Body)
            .ToString();

    private static ValueTask Log(
        IWorkflowContext context, string message, CancellationToken cancellationToken)
        => context.AddEventAsync(
            new WorkflowEvent($"local-picker-to-state: {message}"),
            cancellationToken);
}
