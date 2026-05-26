using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the newsdesk agents for researching
/// and publishing stories.
/// </summary>
public static class NewsDeskTools
{
    [Description("Searches for recent news headlines and summaries about a topic. " +
        "Returns the top 3 results.")]
    [ExportAIFunction("search_news")]
    public static async Task<string> SearchNews(
        string query,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a news search API that returns 3 fictional but plausible-sounding headlines
                for the given topic.

                Each headline should include:
                  - A specific statistic, percentage, or number (e.g. "47% of users",
                    "$2.3B funding", "3,200 affected")
                  - A named source (Reuters, BBC, TechCrunch, Nature, etc.)
                  - A time ago (e.g. "2 hours ago", "5 days ago")
                  - A 1-2 sentence summary with another specific claim or quote from
                    a named expert/official.

                IMPORTANT: Make UP the statistics and quotes. Invent realistic-sounding
                numbers and names. Some claims should be borderline implausible (e.g.
                exaggerated %s, suspiciously round numbers, quotes from people who don't
                exist). This is a fact-checking demo, so the more "checkable" specific
                claims you bury in each item the better.

                Format as a numbered list (1., 2., 3.) with the headline first, then
                the summary.
                """),
            new(ChatRole.User, $"Search news about: {query}")
        ],
        new() { MaxOutputTokens = 400 });
        return response.Text ?? $"No results found for: {query}";
    }

    [Description("Looks up a specific fact or statistic to verify a claim. Returns " +
        "verification status and source.")]
    [ExportAIFunction("verify_fact")]
    public static async Task<string> VerifyFact(
        string claim,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a skeptical fact-checking service. Be tough. Most specific numerical
                claims from news articles do NOT have a verifiable primary source.

                Respond in this format (1-2 sentences total):
                  - VERIFIED: <citation> only if the claim is widely-known and established
                  - PARTIALLY VERIFIED: <what is and is not> if part of the claim checks out
                  - UNVERIFIED: <reason> for specific numbers, percentages, or quotes that
                    lack a clear source
                  - DISPUTED: <conflicting evidence> if other sources contradict the claim

                Bias toward UNVERIFIED or DISPUTED for fabricated-sounding statistics, exact
                percentages, round-number dollar amounts, or quotes from named individuals.
                This is a demo showing what a real fact-checker would catch.
                """),
            new(ChatRole.User, $"Verify this claim: {claim}")
        ],
        new() { MaxOutputTokens = 150 });
        return response.Text
            ?? $"UNVERIFIED: Unable to verify: \"{claim}\"";
    }

    [Description("Formats the final article for publication with proper markup and metadata.")]
    [ExportAIFunction("format_for_publication")]
    public static string FormatForPublication(
        string title, string article, string category)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");
        return $"""
            ---
            title: {title}
            category: {category}
            published: {timestamp}
            status: ready
            ---

            # {title}

            {article}

            ---
            *Published via AI Newsdesk Pipeline | {timestamp}*
            """;
    }
}
