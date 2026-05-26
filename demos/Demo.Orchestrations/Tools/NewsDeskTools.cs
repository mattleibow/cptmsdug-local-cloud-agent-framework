using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the newsdesk agents for researching and publishing stories.
/// </summary>
public static class NewsDeskTools
{
    [Description("Searches for recent news headlines and summaries about a topic. Returns the top 3 results.")]
    [ExportAIFunction("search_news")]
    public static async Task<string> SearchNews(
        string query,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, "You are a news search API. Return exactly 3 recent headlines with source, time ago, and a one-line summary. Format as a numbered list. Be realistic and timely."),
            new(ChatRole.User, $"Search news about: {query}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text ?? $"No results found for: {query}";
    }

    [Description("Looks up a specific fact or statistic to verify a claim. Returns verification status and source.")]
    [ExportAIFunction("verify_fact")]
    public static async Task<string> VerifyFact(
        string claim,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, "You are a fact-checking service. Verify the claim and respond with either 'VERIFIED: [source]' or 'UNVERIFIED: [reason]'. Keep it to 1-2 sentences."),
            new(ChatRole.User, $"Verify this claim: {claim}")
        ],
        new() { MaxOutputTokens = 100 });
        return response.Text ?? $"UNVERIFIED: Unable to verify: \"{claim}\"";
    }

    [Description("Formats the final article for publication with proper markup and metadata.")]
    [ExportAIFunction("format_for_publication")]
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
