using System.ComponentModel;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools for the startup pitch group chat — market research, financial modeling, and competitive analysis.
/// </summary>
public static class StartupTools
{
    [Description("Looks up market size and growth data for a specific industry or segment.")]
    [ExportAIFunction("lookup_market_data")]
    public static string LookupMarketData(
        [Description("The industry or market segment to research")] string market)
    {
        var lower = market.ToLowerInvariant();
        if (lower.Contains("fintech") || lower.Contains("finance") || lower.Contains("personal finance"))
            return """
                📊 Market Data: Personal Finance / Fintech
                ─────────────────────────────────────
                TAM: $1.5 trillion (global financial services)
                SAM: $26B (personal finance apps, 2026)
                SOM: $2.1B (Gen Z segment, ages 18-27)
                Growth: 18% CAGR (2024-2029)
                Key players: Robinhood, Cash App, Revolut, Monzo
                Gen Z adoption: 73% use at least one finance app
                Source: McKinsey Digital Finance Report 2026
                """;
        if (lower.Contains("ai") || lower.Contains("artificial intelligence"))
            return """
                📊 Market Data: AI / Machine Learning
                ─────────────────────────────────────
                TAM: $900B (global AI market, 2026)
                SAM: $180B (enterprise AI applications)
                Growth: 37% CAGR (2024-2030)
                Enterprise adoption: 78% of Fortune 500
                Source: Gartner AI Market Forecast 2026
                """;
        return $"""
            📊 Market Data: {market}
            ─────────────────────────────────────
            TAM: Estimated $50-200B (varies by segment)
            Growth: 12-25% CAGR (estimated)
            Note: Specific data requires deeper industry research.
            Source: General market estimates
            """;
    }

    [Description("Estimates unit economics for a consumer app based on pricing model and target audience.")]
    [ExportAIFunction("estimate_unit_economics")]
    public static string EstimateUnitEconomics(
        [Description("The pricing model (freemium, subscription, transaction-based)")] string pricingModel,
        [Description("Monthly price in USD (for subscription models)")] double monthlyPrice = 9.99)
    {
        var cac = Random.Shared.Next(15, 45);
        var ltv = monthlyPrice * Random.Shared.Next(8, 24);
        var ratio = ltv / cac;
        return $"""
            💰 Unit Economics Estimate ({pricingModel})
            ─────────────────────────────────────
            Customer Acquisition Cost (CAC): ${cac}
            Monthly Revenue/User: ${monthlyPrice:F2}
            Estimated LTV: ${ltv:F2}
            LTV:CAC Ratio: {ratio:F1}x {(ratio > 3 ? "✅ Healthy" : ratio > 1.5 ? "⚠️ Marginal" : "❌ Unsustainable")}
            Payback Period: {cac / monthlyPrice:F0} months
            Churn Rate (Gen Z avg): 8-12%/month
            
            Benchmark: Top fintech apps achieve LTV:CAC > 5x
            """;
    }

    [Description("Searches for competitor information and recent funding rounds in a space.")]
    [ExportAIFunction("search_competitors")]
    public static string SearchCompetitors(
        [Description("The product category or space to search")] string space)
    {
        if (space.ToLowerInvariant().Contains("finance") || space.ToLowerInvariant().Contains("fintech"))
            return """
                🏁 Competitive Landscape: Gen Z Finance Apps
                ─────────────────────────────────────
                • Cleo AI - $120M Series C (2025), 6M users, AI budgeting
                • Greenlight - $260M raised, 5M families, teen finance
                • Step - $100M Series C, 4M teens/young adults
                • Copper Banking - $50M raised, Gen Z debit + savings
                • Plum - £85M raised, AI savings, UK-focused
                
                Gaps identified:
                - Few combine AI + investing + budgeting in one app
                - Limited social/gamification features
                - Most target broad demographics, not Gen Z specifically
                """;
        return $"""
            🏁 Competitive Landscape: {space}
            ─────────────────────────────────────
            • 3-5 well-funded competitors identified
            • Total segment funding: $500M+ in last 2 years
            • Market consolidation expected in 12-18 months
            Note: Detailed competitive analysis recommended.
            """;
    }
}
