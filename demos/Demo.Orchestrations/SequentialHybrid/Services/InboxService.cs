using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Demo.Orchestrations.SequentialHybrid.Models;

namespace Demo.Orchestrations.SequentialHybrid.Services;

/// <summary>
/// Stands in for "the user's email inbox" in the local + cloud demo —
/// addressed to a specific (fictional) user and able to fabricate plausible
/// inbound emails on demand.
///
/// Carries the mailbox owner's identity as instance properties so every
/// downstream stage (inbox generator, cloud-prompt adapter, final assembler)
/// can read consistent values without a static side-channel. The picker
/// schema deliberately does NOT include the recipient — the recipient is
/// always the owner, sourced from here.
///
/// Used by the inbox-picker agent through a <c>TextSearchProvider</c>.
/// Each search result is a single fully-formed email so the inbox-picker
/// only needs to choose between them.
/// </summary>
public sealed class InboxService([FromKeyedServices(AIModels.Local)] IChatClient localChatClient)
{
    /// <summary>The full name of the inbox owner (the human the demo runs for).</summary>
    public string OwnerName { get; } = "Alex Park";

    /// <summary>The inbox owner's email address.</summary>
    public string OwnerEmail { get; } = "alex.park@aurora-labs.com";

    /// <summary>First-name view of the owner, used for greetings / signatures.</summary>
    public string OwnerFirstName =>
        OwnerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? OwnerName;

    /// <summary>
    /// Adapter for <see cref="TextSearchProvider"/>: given the agent's most
    /// recent user question, fabricate 3-5 plausible inbox emails on-device.
    /// Each email becomes one search result.
    /// </summary>
    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var response = await localChatClient.GetResponseAsync<GeneratedInbox>(
        [
            new(ChatRole.System, $$"""
                You are a FAKE INBOX generator for a privacy demo. Invent 3-5
                realistic emails the user has RECEIVED and would want to reply
                to. Vary the senders.

                The user (the inbox owner / always the RECIPIENT) is:
                  RECIPIENT_NAME:  {{OwnerName}}
                  RECIPIENT_EMAIL: {{OwnerEmail}}

                For each generated email, fill the schema fields:
                  senderName      = a colleague's full name (not the user)
                  senderEmail     = that colleague's email
                  recipientName   = "{{OwnerName}}"   (always)
                  recipientEmail  = "{{OwnerEmail}}"  (always)
                  subject         = short, work-style subject line
                  received        = an ISO timestamp within the last 7 days
                  body            = the email body, written by the sender,
                                    opening with "Hi {{OwnerFirstName}},"

                Each body must mention:
                  - one full person name (first + last) other than the user
                  - one company / organisation
                  - one project / product
                  - one specific dollar amount

                Each body is 2-4 short paragraphs, in a real work-email tone.
                """),
            new(ChatRole.User, $"What the user wants to reply about: {query}")
        ],
        new ChatOptions { MaxOutputTokens = 1500 },
        cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.TryGetResult(out var inbox) || inbox.Emails is null)
            return [];

        return inbox.Emails.Select(static (e, i) => new TextSearchProvider.TextSearchResult
        {
            SourceName = $"Inbox #{i + 1}: {e.Subject}",
            // Field order matches PickedEmail's schema order: email before
            // name. The email's local-part / domain is a strong lexical
            // anchor that pulls the small on-device model toward the
            // correct name when it emits the SenderName field next.
            Text = $$"""
                SENDER_EMAIL: {{e.SenderEmail}}
                SENDER_NAME:  {{e.SenderName}}
                SUBJECT:      {{e.Subject}}
                RECEIVED:     {{e.Received}}

                {{e.Body}}
                """,
        });
    }

    private sealed record GeneratedInbox(IReadOnlyList<GeneratedEmail> Emails);

    // RecipientName and RecipientEmail are still generated so the body can
    // address the user ("Hi Alex, …") but they are NOT exposed to the picker
    // — the recipient is always the inbox owner, sourced from this service.
    private sealed record GeneratedEmail(
        string SenderName,
        string SenderEmail,
        string RecipientName,
        string RecipientEmail,
        string Subject,
        string Received,
        string Body);
}
