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
                You are a news search API that returns 3 headlines about the given topic.
                Mix REAL and OUTRAGEOUSLY FAKE so a fact-checker can demo telling them apart.

                Generate 3 items in this exact mix:

                  1. REAL — a headline based on something that actually happened or is
                     widely-reported about this topic. Use real-sounding numbers, real
                     organisations, real people. Should pass a fact-check.

                  2. PLAUSIBLE BUT INVENTED — sounds like real wire copy but the specifics
                     (exact %, dollar figure, named expert) are made up. A fact-checker
                     might mark it PARTIALLY VERIFIED or UNVERIFIED.

                  3. OUTRAGEOUSLY FAKE — clearly absurd. Examples: aliens contacted MIT,
                     dragons spotted, $50 trillion fund announced, breakthrough achieved by
                     a 7-year-old, government bans the colour blue. Have fun — the fact-
                     checker should easily flag it as DISPUTED.

                For every item include:
                  - A specific stat / number / dollar amount
                  - A named source (Reuters, BBC, TechCrunch, Nature, Bloomberg, The Onion…)
                  - A time ago (e.g. "2 hours ago", "5 days ago")
                  - A 1-2 sentence summary with another claim or quote attributed to someone

                Format as a numbered list (1., 2., 3.) — headline first, then summary. Do NOT
                tell the user which item is which kind; let the fact-checker sort it out.
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
                You are a wire-service fact-checker. Quick, decisive judgments — not academic
                peer review. You're checking news copy against your general world knowledge.

                Pick the verdict that best matches the claim:

                  - VERIFIED: <one-line plausible source>
                    The claim sounds correct and matches widely-reported facts. Cite a
                    realistic-sounding source. ~60-70% of well-formed real-world claims
                    should land here.

                  - PARTIALLY VERIFIED: <what's confirmed, what's not>
                    The broad point is right but a specific number or named person can't
                    be pinned down exactly.

                  - UNVERIFIED: <reason>
                    A specific stat or quote has no obvious source, but it's not absurd.

                  - DISPUTED: <one-line "why this is clearly wrong" reason>
                    The claim is OUTRAGEOUS or contradicts well-known facts. Examples:
                    "aliens contacted MIT", "$50 trillion fund", "dragons spotted",
                    "7-year-old wins Nobel Prize", "government bans colour blue".
                    Be decisive and a little dry. Don't hedge — call out the absurdity.

                Be confident. 1-2 sentences total. NEVER refuse — always pick a verdict.
                """),
            new(ChatRole.User, $"Verify this claim: {claim}")
        ],
        new() { MaxOutputTokens = 150 });
        return response.Text
            ?? $"VERIFIED: \"{claim}\" — consistent with general reporting on the topic.";
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
