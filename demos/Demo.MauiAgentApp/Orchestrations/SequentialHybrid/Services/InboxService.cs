using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Models;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;

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
    ///
    /// Picks a fresh subset of <see cref="IssueThemes"/> per call and bumps
    /// sampling temperature so demo runs don't all converge on the same
    /// "account locked out + password leak" pattern.
    /// </summary>
    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var themes = PickThemes(count: 5);
        var sensitiveMix = PickSensitiveDetails(count: 4);

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

                IMPORTANT — issue variety:
                For THIS batch, the inbox MUST cover the following kinds of
                problems (one email per theme, in any order — do not invent
                a different theme):

                {{string.Join("\n                ", themes.Select(t => "  • " + t))}}

                Vary the customer names AND email domains so the inbox
                feels like it belongs to a real, varied user base — do
                not reuse names across themes. Bias toward names from
                different cultures.

                For each generated email, fill the schema fields:
                  senderName      = a customer's full name (not the user)
                  senderEmail     = that customer's email (vary the domains)
                  recipientName   = "{{OwnerName}}"   (always)
                  recipientEmail  = "{{OwnerEmail}}"  (always)
                  subject         = short, theme-appropriate subject line —
                                    NOT all of them should start with the
                                    same word
                  received        = an ISO timestamp within the last 7 days
                  body            = the email body, written by the customer,
                                    opening with "Hi {{OwnerFirstName}},"

                The body MUST mix in sensitive details — the kind of stuff
                customers genuinely put in support emails because they
                think it'll help support resolve the issue faster.

                For THIS batch, weave in AT LEAST the following per body,
                only when they fit the issue:

                {{string.Join("\n                ", sensitiveMix.Select(d => "  • " + d))}}

                Use only realistic-looking but obviously fake values
                (cards starting 4111, SSNs starting 999-, addresses on
                fake streets in real cities). Never use real cards or SSNs.

                Each body is 2-3 SHORT paragraphs (≤ 600 characters total)
                in a real customer-support tone — frustrated but polite,
                concrete about what went wrong, asking for resolution.
                Avoid template-y openings.
                """),
            new(ChatRole.User, $"What the user wants to reply about: {query}")
        ],
        new ChatOptions
        {
            MaxOutputTokens = 1500,
            // Bump variety so subsequent runs aren't carbon copies of each
            // other. Demo-only — production fabricators would not do this.
            Temperature = 1.0f,
            TopP = 0.95f,
        },
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

    /// <summary>
    /// Pool of customer-support themes the fake inbox can pull from. The
    /// generator picks a fresh random subset per call so the demo doesn't
    /// turn into "five account-lockout emails forever".
    /// </summary>
    private static readonly string[] IssueThemes =
    [
        "Account locked out after repeated failed login attempts",
        "Duplicate charge on a recent order — customer wants a refund",
        "Broken third-party integration (Slack / Zapier / GitHub / etc.)",
        "Suspected fraudulent activity on the customer's account",
        "Lost shipment or wrong item delivered for a fulfilled order",
        "Bulk data export request (GDPR-style) for the customer's account",
        "License / SSO entitlements not applying after seat upgrade",
        "API rate-limit confusion — usage doesn't match invoice",
        "Feature regression after recent release — customer reports a bug",
        "Subscription downgrade / upgrade did not take effect at renewal",
        "Sales-tax or VAT line on invoice the customer disputes",
        "Onboarding question — customer's team can't see a shared workspace",
        "Two-factor authentication device lost, urgent re-enrolment",
        "Webhook deliveries silently failing for the last few days",
    ];

    private static readonly string[] SensitiveDetailKinds =
    [
        "the customer's full name + an order/account/case ID (KEEP these in any summary — support needs them)",
        "a physical mailing address (street, city, state/postcode)",
        "a phone number formatted with country code",
        "a password they typed in by mistake (\"I tried Sunshine2024!\")",
        "a credit-card number (always obviously fake: 4111-..., never a real card)",
        "a specific dollar amount (refund owed, charge disputed, balance)",
        "an SSN-style government ID (XXX-XX-XXXX, always fake)",
        "a date of birth used for identity verification",
        "an internal API key or token the customer pasted while debugging",
        "an IP address the customer was logging in from",
    ];

    private static string[] PickThemes(int count) =>
        [.. Shuffle(IssueThemes).Take(count)];

    private static string[] PickSensitiveDetails(int count) =>
        // Always keep the name + ID slot so the summary has something to
        // preserve; pick the rest randomly from the remaining pool.
        [SensitiveDetailKinds[0], .. Shuffle(SensitiveDetailKinds.Skip(1)).Take(count - 1)];

    private static IEnumerable<T> Shuffle<T>(IEnumerable<T> source)
    {
        var array = source.ToArray();
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
        return array;
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
