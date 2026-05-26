using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Tools used by the sequential newsdesk workflow
// ──────────────────────────────────────────────────────────────────────────────

public static class NewsDeskTools
{
    [Description("Searches for recent news headlines and summaries about a topic. " +
        "Returns 3 results.")]
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

    [Description("Publishes a finished article to the newsroom CMS. Returns the live URL.")]
    [ExportAIFunction("publish_article")]
    public static string PublishArticle(
        [Description("The article headline / title")] string title,
        [Description("The category slug (e.g. 'tech', 'health', 'business')")] string category)
    {
        var slug = (title ?? "untitled")
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("'", "")
            .Replace("\"", "");
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        var stamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        return $":check: Published to https://newsroom.example/{category}/{stamp}/{slug}";
    }
}

[AIToolSource(typeof(NewsDeskTools))]
public partial class NewsDeskToolContext : AIToolContext { }

// ──────────────────────────────────────────────────────────────────────────────
// Sequential newsdesk workflow: Reporter → Fact-checker → Editor
// ──────────────────────────────────────────────────────────────────────────────

public static class SequentialWorkflow
{
    public static void AddSequentialWorkflow(this IHostApplicationBuilder builder)
    {
        var newsTools = NewsDeskToolContext.Default.Tools;

        builder.AddAIAgent(
            "sequential-newsdesk-reporter",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "News reporter that researches topics and writes articles.",
                instructions: """
                    You are a news reporter. ALWAYS call the search_news tool FIRST to gather
                    sources. Then write a concise news article (200 words) that incorporates AT
                    LEAST 3 specific claims from the search results — include the statistics,
                    percentages, dollar amounts, and named quotes verbatim. Do NOT hedge or
                    rewrite specific numbers — keep them exactly as the source provided so the
                    fact-checker can verify them.

                    FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                    ## :news: <Compelling Headline Here>

                    *<Italic byline / dateline line>*

                    <Lead paragraph — 2-3 sentences with the most important fact and a stat.>

                    <Supporting paragraph — 2-3 sentences with another statistic and a quote.>

                    <Closing paragraph — context or implication.>
                    """,
                tools: [.. newsTools.Where(t => t.Name == "search_news")]
            ));

        builder.AddAIAgent(
            "sequential-newsdesk-factchecker",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Fact-checker that verifies claims in articles.",
                instructions: """
                    You are a fact-checker. You MUST call the verify_fact tool for AT LEAST 3
                    specific claims in the article (statistics, percentages, dollar amounts,
                    named quotes, dates). Extract each claim verbatim and pass it to verify_fact.

                    After all checks, write a fact-check report.

                    FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                    ## :search: Fact-check report

                    - :check: **VERIFIED:** "<claim>" — <one-line source/reasoning>
                    - :warning: **PARTIALLY VERIFIED:** "<claim>" — <what's confirmed, what's not>
                    - :fail: **UNVERIFIED:** "<claim>" — <why no source could be found>
                    - :error: **DISPUTED:** "<claim>" — <contradicting evidence>

                    **Recommendation:** <1-2 sentence summary of what should be removed,
                    rewritten, or sourced before publication.>
                    """,
                tools: [.. newsTools.Where(t => t.Name == "verify_fact")]
            ));

        builder.AddAIAgent(
            "sequential-newsdesk-editor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Senior editor that polishes and publishes articles.",
                instructions: """
                    You are a senior editor. Take the reporter's article and the fact-checker's
                    report. Rewrite the article to remove or correct any UNVERIFIED / DISPUTED
                    claims, tighten prose, and sharpen the headline.

                    Output the FINAL polished article inline as Markdown (same shape as the
                    reporter used — ## headline, dateline, paragraphs). After the article,
                    call the publish_article tool EXACTLY ONCE with the title and a category
                    slug to push it to the newsroom CMS. After the tool returns DO NOT emit
                    anything else.

                    EXAMPLE STRUCTURE:

                    ## :news: <Polished Headline>

                    *<Dateline>*

                    <Article paragraphs.>

                    (then call publish_article and stop)
                    """,
                tools: [.. newsTools.Where(t => t.Name == "publish_article")]
            ));

        builder.AddWorkflow("sequential-newsdesk", (sp, key) =>
        {
            var agents = new[]
            {
                "sequential-newsdesk-reporter",
                "sequential-newsdesk-factchecker",
                "sequential-newsdesk-editor",
            }
            .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
            .ToArray();
            var workflow = AgentWorkflowBuilder.BuildSequential(key, agents);
            workflow.SetDescription(
                "A reporter researches and writes, a fact-checker verifies claims, and an " +
                "editor polishes and publishes.");
            return workflow;
        }).AddAsAIAgent();
    }
}
