using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

namespace Demo.MauiAgentApp.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Tools used by the startup group chat workflow
// ──────────────────────────────────────────────────────────────────────────────

public static class StartupTools
{
    [Description(
        """
        Looks up market size and growth data for a specific industry or segment.
        """)]
    [ExportAIFunction("lookup_market_data")]
    public static async Task<string> LookupMarketData(
        [Description("The industry or market segment")] string market,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a market research API. Return:
                  - TAM, SAM, SOM figures
                  - Growth rate (CAGR)
                  - Key players
                  - A source citation
                Use realistic numbers. Use headers and bullets.
                """),
            new(ChatRole.User, $"Market data for: {market}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? $"No market data available for: {market}";
    }

    [Description(
        """
        Estimates unit economics for a consumer app based on pricing model and target audience.
        """)]
    [ExportAIFunction("estimate_unit_economics")]
    public static async Task<string> EstimateUnitEconomics(
        [Description("Pricing model: freemium, subscription, transaction")] string pricingModel,
        [Description("Monthly price in USD (for subscriptions)")] double monthlyPrice,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a financial modeling API. Calculate:
                  - CAC (customer acquisition cost)
                  - LTV (lifetime value)
                  - LTV:CAC ratio
                  - Payback period
                  - Churn rate estimate
                Say if economics are healthy, marginal, or unsustainable. Use realistic
                SaaS/consumer benchmarks.
                """),
            new(ChatRole.User, $"Unit economics for {pricingModel} model at ${monthlyPrice}/month")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? "Unable to estimate unit economics";
    }

    [Description(
        """
        Searches for competitor information and recent funding rounds in a space.
        """)]
    [ExportAIFunction("search_competitors")]
    public static async Task<string> SearchCompetitors(
        [Description("Product category or space to search")] string space,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a competitive intelligence API. Return:
                  - 4-5 competitors with funding amounts, user counts, and key differentiators
                  - 2-3 market gaps
                Be realistic and specific to the space.
                """),
            new(ChatRole.User, $"Search competitors in: {space}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text ?? $"No competitor data available for: {space}";
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Group chat workflow: Founder ↔ Investor ↔ Advisor (round-robin)
// ──────────────────────────────────────────────────────────────────────────────

public static partial class GroupChatWorkflow
{
    [AIToolSource(typeof(StartupTools))]
    private partial class StartupToolContext : AIToolContext { }

    public static void AddGroupChatWorkflow(this IHostApplicationBuilder builder)
    {
        var startupTools = StartupToolContext.Default.Tools;

        builder.AddAIAgent(
            "groupchat-startup-founder",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Startup founder pitching and defending their vision.",
                instructions: """
                    You are a startup founder pitching to a VC investor and an advisor.

                    On your FIRST turn, briefly introduce your startup idea — the hook.
                    On every subsequent turn, respond directly to whatever the investor
                    or advisor just said: defend criticisms, answer questions, refine the
                    pitch, and concede valid points. Use lookup_market_data when a real
                    number would strengthen your answer.

                    Keep each turn to ~80 words. Don't repeat your introduction. Don't
                    promise anything for a "next turn" — the facilitator decides who
                    speaks when and when the meeting ends.

                    Write plain prose; the UI already labels your messages. Use **bold**
                    to emphasise key claims.
                    """,
                tools: [.. startupTools.Where(t => t.Name == "lookup_market_data")]
            ).WithTelemetry());

        builder.AddAIAgent(
            "groupchat-startup-investor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "VC investor evaluating the pitch with tough questions.",
                instructions: """
                    You are a VC investor evaluating a startup pitch.

                    On your FIRST turn, give an initial reaction plus the toughest
                    question that comes to mind. On every subsequent turn, follow up on
                    the founder's response — dig into the weakest answer, acknowledge
                    what's solid, and raise a NEW concern. Use estimate_unit_economics
                    or search_competitors to challenge claims with data when useful.

                    Keep each turn to ~80 words. Never repeat previous questions. Don't
                    promise anything for a "next turn" — the facilitator decides who
                    speaks when and when the meeting ends.

                    Write plain prose; the UI already labels your messages. Use **bold**
                    for tough questions or red flags.
                    """,
                tools: [.. startupTools.Where(t =>
                    t.Name is "estimate_unit_economics" or "search_competitors")]
            ).WithTelemetry());

        builder.AddAIAgent(
            "groupchat-startup-advisor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Seasoned advisor mediating and delivering a closing recap.",
                instructions: """
                    You are a seasoned startup advisor in a 3-round round-robin pitch
                    meeting. Each round one of you speaks — founder, then investor, then
                    you. After your 3rd turn the meeting ends, so YOUR LAST TURN IS THE
                    SUMMARY.

                    Look at the conversation so far and count how many times you've already
                    spoken in this thread:

                    - **If you've spoken 0 times:** initial framing — what's interesting
                      about the idea, what's risky. ~80 words.
                    - **If you've spoken 1 time:** mediate the tension between founder
                      and investor; suggest one concrete pivot or compromise. ~80 words.
                    - **If you've spoken 2 times (this is your final turn):** wrap the
                      meeting up with a clear, actionable recap. Use this EXACT structure
                      (Markdown) and ~200 words:

                        ### Closing recap

                        **Strengths**

                        - <one-line strength>
                        - <one-line strength>

                        **Risks / red flags**

                        - <one-line risk>
                        - <one-line risk>

                        **Top 3 next steps**

                        1. <concrete action>
                        2. <concrete action>
                        3. <concrete action>

                        **Verdict:** <one sentence — "promising but...", "pass unless...",
                        "strong, invest at seed", etc.>

                    NEVER say "summary coming next" or "more to follow" — every turn
                    must stand on its own. By your 3rd turn you must deliver the recap.
                    Use lookup_market_data if you need a number to back up a recommendation.

                    Write plain prose; the UI already labels your messages. Use **bold**
                    for actionable suggestions.
                    """,
                tools: [.. startupTools.Where(t => t.Name == "lookup_market_data")]
            ).WithTelemetry());

        builder.AddWorkflow("groupchat-startup", (sp, key) =>
        {
            var participants = new[]
            {
                "groupchat-startup-founder",
                "groupchat-startup-investor",
                "groupchat-startup-advisor",
            }
            .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
            .ToArray();

            return AgentWorkflowBuilder
                .CreateGroupChatBuilderWith(agents =>
                    new RoundRobinGroupChatManager(agents)
                    {
                        // 3 agents × 3 rounds = 9 turns total.
                        MaximumIterationCount = 9,
                    })
                .AddParticipants(participants)
                .WithName(key)
                .WithDescription(
                    "Founder, investor, and advisor take turns over 3 rounds (round-robin). " +
                    "The advisor knows the round count and delivers the closing recap on " +
                    "their final turn.")
                .Build();
        }).AddAsAIAgent();
    }
}
