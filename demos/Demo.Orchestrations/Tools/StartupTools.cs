using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools for the startup pitch group chat — market research, financial modeling, and competitive analysis.
/// </summary>
public static class StartupTools
{
    [Description("Looks up market size and growth data for a specific industry or segment.")]
    [ExportAIFunction("lookup_market_data")]
    public static async Task<string> LookupMarketData(
        [Description("The industry or market segment to research")] string market,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, "You are a market research API. Return TAM, SAM, SOM figures, growth rate (CAGR), key players, and a source citation. Use realistic numbers. Format with headers and bullet points."),
            new(ChatRole.User, $"Market data for: {market}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? $"No market data available for: {market}";
    }

    [Description("Estimates unit economics for a consumer app based on pricing model and target audience.")]
    [ExportAIFunction("estimate_unit_economics")]
    public static async Task<string> EstimateUnitEconomics(
        [Description("The pricing model (freemium, subscription, transaction-based)")] string pricingModel,
        [Description("Monthly price in USD (for subscription models)")] double monthlyPrice,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, "You are a financial modeling API. Calculate and return CAC, LTV, LTV:CAC ratio, payback period, and churn rate estimate. Include whether the economics are healthy, marginal, or unsustainable. Use realistic SaaS/consumer app benchmarks."),
            new(ChatRole.User, $"Estimate unit economics for {pricingModel} model at ${monthlyPrice}/month")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? "Unable to estimate unit economics";
    }

    [Description("Searches for competitor information and recent funding rounds in a space.")]
    [ExportAIFunction("search_competitors")]
    public static async Task<string> SearchCompetitors(
        [Description("The product category or space to search")] string space,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, "You are a competitive intelligence API. Return 4-5 competitors with their funding amounts, user counts, and key differentiators. Then list 2-3 market gaps. Be realistic and specific to the space."),
            new(ChatRole.User, $"Search competitors in: {space}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text ?? $"No competitor data available for: {space}";
    }
}
