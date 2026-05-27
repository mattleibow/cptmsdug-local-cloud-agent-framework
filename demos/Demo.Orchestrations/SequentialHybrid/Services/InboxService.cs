using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Demo.Orchestrations.SequentialHybrid.Models;

namespace Demo.Orchestrations.SequentialHybrid.Services;

/// <summary>
/// Stands in for "the user's email inbox" in the local + cloud demo.
///
/// Rather than carry a hard-coded list of emails, this service asks the
/// on-device chat client to invent 3-5 plausible emails on demand, picks
/// the one most relevant to the user's request, and returns it as a
/// structured <see cref="PickedEmail"/>.
///
/// Used by the inbox-picker agent through a <c>TextSearchProvider</c>.
/// Each search result is a single fully-formed email so the inbox-picker
/// only needs to choose between them.
/// </summary>
public sealed class InboxService
{
    private readonly IChatClient _local;

    public InboxService([FromKeyedServices(AIModels.Local)] IChatClient localChatClient)
    {
        _local = localChatClient;
    }

    /// <summary>
    /// Adapter for <see cref="TextSearchProvider"/>: given the agent's most
    /// recent user question, fabricate 3-5 plausible inbox emails on-device.
    /// Each email becomes one search result.
    /// </summary>
    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var response = await _local.GetResponseAsync<GeneratedInbox>(
        [
            new(ChatRole.System, $$"""
                You are a FAKE INBOX generator for a privacy demo. The user is
                drafting a reply about a specific topic. Invent 3-5 realistic
                emails that the user has RECEIVED in their inbox and would
                plausibly want to reply to.

                The user is: "{{UserProfile.Name}}" <{{UserProfile.Email}}>.

                CRITICAL — every email is INBOUND to the user:
                  - "to"   field MUST be the user (toName="{{UserProfile.Name}}",
                    toEmail="{{UserProfile.Email}}")
                  - "from" field MUST be a DIFFERENT person (a colleague, vendor,
                    customer, etc.) — NEVER the user themselves
                  - the body MUST be written FROM the colleague's perspective,
                    addressed TO the user. Typical opening: "Hi {{UserProfile.Name.Split(' ')[0]}}, …"
                  - the body MUST NOT be written by the user — never "Hi <colleague-name>"
                    where the colleague is the from-person

                CRITICAL — each email body MUST contain AT LEAST one of EACH
                of the following so the privacy redactor has substance:
                  - One full person name (first + last) other than the user
                  - One company / organisation name
                  - One project or product name
                  - One specific dollar amount

                You may include MORE of any of these (e.g. several names,
                several amounts) — but never fewer than one of each.

                Each email body should be 2-4 short paragraphs. Avoid
                template-y openings; sound like real work email.

                Return JSON matching the schema.
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
            Text = $$"""
                FROM_NAME:    {{e.FromName}}
                FROM_EMAIL:   {{e.FromEmail}}
                TO_NAME:      {{e.ToName}}
                TO_EMAIL:     {{e.ToEmail}}
                SUBJECT:      {{e.Subject}}
                RECEIVED:     {{e.Received}}

                {{e.Body}}
                """,
        });
    }

    private sealed record GeneratedInbox(IReadOnlyList<GeneratedEmail> Emails);

    private sealed record GeneratedEmail(
        string FromName,
        string FromEmail,
        string ToName,
        string ToEmail,
        string Subject,
        string Received,
        string Body);
}
