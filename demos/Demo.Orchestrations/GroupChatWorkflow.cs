using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

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
                    You are a startup founder in a pitch meeting. On your FIRST turn, briefly
                    introduce your startup idea. On SUBSEQUENT turns, respond directly to what
                    the investor or advisor just said — defend criticisms, answer questions,
                    incorporate feedback, and refine your pitch. You can use lookup_market_data
                    to back up claims with real numbers. Never repeat your introduction. Build
                    on the conversation. Keep each contribution to 80 words.

                    FORMAT: start every contribution with "**:rocket: Founder:**" then a blank
                    line then your message. Use **bold** to emphasise key claims.
                    """,
                tools: [.. startupTools.Where(t => t.Name == "lookup_market_data")]
            ));

        builder.AddAIAgent(
            "groupchat-startup-investor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "VC investor evaluating the pitch with tough questions.",
                instructions: """
                    You are a VC investor in a pitch meeting. On your FIRST turn, react to the
                    founder's pitch with an initial assessment. On SUBSEQUENT turns, follow up
                    on previous answers — dig deeper into weak points, acknowledge good
                    responses, and raise NEW concerns you haven't mentioned yet. Use
                    estimate_unit_economics and search_competitors to challenge claims with
                    data. Never repeat previous questions. Keep contributions to 80 words.

                    FORMAT: start every contribution with "**:chart: Investor:**" then a blank
                    line then your message. Use **bold** for tough questions or red flags.
                    """,
                tools: [.. startupTools.Where(t => t.Name is "estimate_unit_economics" or "search_competitors")]
            ));

        builder.AddAIAgent(
            "groupchat-startup-advisor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Seasoned advisor bridging optimism and skepticism.",
                instructions: """
                    You are a seasoned startup advisor in a pitch meeting. On your FIRST turn,
                    share initial thoughts. On SUBSEQUENT turns, mediate between the founder
                    and investor — acknowledge valid points from both, suggest compromises or
                    pivots, and on your final turn provide a brief summary of actionable next
                    steps. You can use lookup_market_data to ground recommendations in data.
                    Keep contributions to 80 words.

                    FORMAT: start every contribution with "**:lightbulb: Advisor:**" then a
                    blank line then your message. Use **bold** for actionable suggestions.
                    """,
                tools: [.. startupTools.Where(t => t.Name == "lookup_market_data")]
            ));

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
                        MaximumIterationCount = 9
                    })
                .AddParticipants(participants)
                .WithName(key)
                .WithDescription("Founder pitches, investor challenges, advisor mediates — 3 rounds.")
                .Build();
        }).AddAsAIAgent();
    }
}
