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
/// The fabrication itself uses the CLOUD chat client: this isn't part of
/// the privacy-sensitive path (the "user's data" doesn't exist yet, we're
/// inventing it). Using the cloud model gets us denser, more varied
/// content with multiple names / companies / projects / amounts per email,
/// which gives the on-device redactor more to work with in the demo.
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
public sealed class InboxService([FromKeyedServices(AIModels.Cloud)] IChatClient generatorChatClient)
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
        var response = await generatorChatClient.GetResponseAsync<GeneratedInbox>(
        [
            new(ChatRole.System, $$"""
                You are a FAKE INBOX generator for a privacy demo. The user
                runs a customer-success helpdesk. Invent 3-5 realistic
                INBOUND emails from customers reporting issues or asking
                for help — the kind of email the user would want to triage
                into a meeting invite.

                The user (the inbox owner / always the RECIPIENT) is:
                  RECIPIENT_NAME:  {{OwnerName}}
                  RECIPIENT_EMAIL: {{OwnerEmail}}

                For each generated email, fill the schema fields:
                  senderName      = a customer's full name (not the user)
                  senderEmail     = that customer's email (vary the domains)
                  recipientName   = "{{OwnerName}}"   (always)
                  recipientEmail  = "{{OwnerEmail}}"  (always)
                  subject         = short, "Issue with X" / "Cannot access Y"
                                    / "Need help with Z" style subject
                  received        = an ISO timestamp within the last 7 days
                  body            = the email body, written by the customer,
                                    opening with "Hi {{OwnerFirstName}},"

                Each body MUST describe an actual problem the customer
                needs help with (account locked out, charge dispute, broken
                integration, data export, fraud alert, etc.) AND naturally
                mix in a varied set of sensitive details — the kind of
                stuff customers genuinely put in support emails because
                they think it'll help support resolve the issue faster.

                Include AT LEAST FOUR of the following per body, chosen so
                the issue makes sense:

                  - The customer's full name and order/account/case ID
                    (these MUST stay in the summary — support needs them)
                  - The customer's physical mailing address
                  - A phone number (e.g. +1 555-123-4567)
                  - A password they typed into the email by mistake
                    (e.g. "I tried logging in with Sunshine2024!")
                  - A credit-card number (e.g. 4111-2222-3333-4444, always
                    obviously fake, never a real card)
                  - A specific dollar amount (refund, charge, balance)
                  - A US SSN-style ID (XXX-XX-XXXX, always fake)

                Mix and match — a charge dispute might include order ID
                + card number + amount + phone; a login issue might include
                account ID + password + address. Use only realistic-looking
                but obviously fake values.

                Each body is 2-3 SHORT paragraphs (≤ 600 characters total)
                in a real customer-support tone — frustrated but polite,
                concrete about what went wrong, asking for resolution.
                Avoid template-y openings.
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
