using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Orchestrations.Services;

/// <summary>
/// Stands in for "the user's email inbox" in the local + cloud demo.
///
/// Instead of carrying a hard-coded list of emails around, this service
/// generates 3–5 plausible-looking emails on demand by asking the
/// <b>on-device</b> chat client to invent them.
///
/// That keeps three demo points visible:
///   1. Real on-device inference (Apple Intelligence) — fabricating the inbox.
///   2. The cloud agent never sees the raw "private" emails — only the
///      redacted version produced by a later local stage.
///   3. RAG plumbing is just <c>TextSearchProvider</c> wired to
///      <see cref="SearchAsync"/>.
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
    /// recent user question, fabricate 3–5 plausible inbox emails on-device.
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
                emails from their inbox that would plausibly be relevant.

                The user (the inbox owner) is:
                  Name:  {{UserProfile.Name}}
                  Email: {{UserProfile.Email}}

                Every generated email should have its 'to' field set to the
                user's email address ({{UserProfile.Email}}). The 'from' field
                must be someone OTHER than the user.

                Mix real-sounding names, companies, projects, dollar amounts,
                and dates so a downstream redactor has something to strip.
                Pull from a varied cast: Alice Chen, Bob Martinez, Sarah Kim,
                Maria Rodriguez, James Park; companies like Acme Corp, Globex
                Industries, Northwind Traders, Contoso Ltd; project names
                like Project Atlas, Project Phoenix.

                The MOST RELEVANT email should be first. Each body is 1-3
                short paragraphs that mention specific names, companies,
                and concrete details. Body should sound like a real email
                addressed TO the user — e.g. starting "Hi {{UserProfile.Name.Split(' ')[0]}}," or "Hey,".
                """),
            new(ChatRole.User, $"Topic the user wants to reply about: {query}")
        ],
        new ChatOptions { MaxOutputTokens = 1200 },
        cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.TryGetResult(out var inbox) || inbox.Emails is null)
            return [];

        return inbox.Emails.Select(static (e, i) => new TextSearchProvider.TextSearchResult
        {
            SourceName = $"Inbox #{i + 1}: {e.Subject}",
            Text = $"""
                From: {e.From}
                To: {e.To ?? UserProfile.Email}
                Subject: {e.Subject}
                Received: {e.Received}

                {e.Body}
                """,
        });
    }

    private sealed record GeneratedInbox(IReadOnlyList<GeneratedEmail> Emails);

    private sealed record GeneratedEmail(
        string From,
        string? To,
        string Subject,
        string Received,
        string Body);
}
