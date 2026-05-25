using System.ComponentModel;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the newsdesk agents for researching and publishing stories.
/// </summary>
public static class NewsDeskTools
{
    [Description("Searches for recent news headlines and summaries about a topic. Returns the top 3 results.")]
    public static string SearchNews(string query)
    {
        // Simulated news search results for demo purposes
        var results = query.ToLowerInvariant() switch
        {
            var q when q.Contains("ai") || q.Contains("artificial intelligence") => """
                1. "Global AI Safety Summit Reaches Historic Agreement" - Reuters, 2 hours ago
                   Leaders from 40 nations signed a framework for responsible AI development.
                2. "OpenAI Announces GPT-5 with Enhanced Reasoning" - TechCrunch, 5 hours ago
                   The new model shows significant improvements in multi-step problem solving.
                3. "EU AI Act Enforcement Begins Next Month" - BBC, 1 day ago
                   Companies have 30 days to comply with the new transparency requirements.
                """,
            var q when q.Contains("climate") || q.Contains("environment") => """
                1. "Record-Breaking Heatwave Hits Southern Europe" - AP News, 3 hours ago
                   Temperatures exceeded 45°C across Spain, Italy, and Greece.
                2. "New Carbon Capture Plant Opens in Iceland" - Nature, 6 hours ago
                   The facility can remove 36,000 tonnes of CO2 annually.
                3. "Global Renewable Energy Investment Surges 25%" - Financial Times, 1 day ago
                   Solar and wind now account for 35% of global electricity generation.
                """,
            _ => $"""
                1. "Breaking: Major Development in {query}" - AP News, 1 hour ago
                   Officials confirmed significant progress on the matter.
                2. "Expert Analysis: What {query} Means for the Future" - Reuters, 4 hours ago
                   Industry analysts weigh in on the implications.
                3. "Public Response to {query} Developments" - BBC, 8 hours ago
                   Community reactions range from cautious optimism to concern.
                """
        };
        return results;
    }

    [Description("Looks up a specific fact or statistic to verify a claim. Returns verification status and source.")]
    public static string VerifyFact(string claim)
    {
        // Simulated fact verification for demo
        if (claim.Contains("40 nations") || claim.Contains("42 nations") || claim.Contains("30 countries"))
            return "VERIFIED: The summit included representatives from 42 nations (source: UN Press Release, May 2026)";
        if (claim.Contains("45°C") || claim.Contains("record") || claim.Contains("heatwave"))
            return "VERIFIED: Temperature records confirmed by EU Copernicus Climate Service";
        if (claim.Contains("36,000") || claim.Contains("carbon capture"))
            return "VERIFIED: Capacity confirmed in Climeworks official press release";
        if (claim.Contains("25%") || claim.Contains("renewable"))
            return "VERIFIED: IEA World Energy Outlook 2026 confirms 25% YoY growth";
        return $"UNVERIFIED: No authoritative source found for: \"{claim}\". Recommend citing as 'reports indicate' or removing.";
    }

    [Description("Formats the final article for publication with proper markup and metadata.")]
    public static string FormatForPublication(string title, string article, string category)
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
