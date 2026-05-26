using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools for the travel planning agents.
/// </summary>
public static class TravelTools
{
    [Description("Searches for restaurants and food experiences at a destination. " +
        "Returns top-rated options with prices.")]
    [ExportAIFunction("search_restaurants")]
    public static async Task<string> SearchRestaurants(
        [Description("The travel destination city")]
        string destination,
        [Description("Type of cuisine (optional)")]
        string? cuisine,
        [FromServices] IChatClient chatClient)
    {
        var cuisineHint = cuisine != null
            ? $" focusing on {cuisine} cuisine"
            : "";
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a restaurant search API. Return 4-5
                restaurant recommendations with:
                  - Name
                  - Price range (use local currency)
                  - Rating (⭐)
                  - Cuisine type
                Be realistic for the destination.
                """),
            new(ChatRole.User,
                $"Find restaurants in {destination}{cuisineHint}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text
            ?? $"No restaurants found in {destination}";
    }

    [Description("Checks transport options and approximate costs between locations at " +
        "the destination.")]
    [ExportAIFunction("check_transport")]
    public static async Task<string> CheckTransport(
        [Description("The travel destination city")]
        string destination,
        [Description("Starting point within the city")]
        string from,
        [Description("End point within the city")]
        string to,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a transport information API. Return 3-4
                transport options (taxi/rideshare, public transit,
                walking, rental) with estimated time, cost, and
                a local tip. Use local currency.
                """),
            new(ChatRole.User,
                $"Transport from {from} to {to} in {destination}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text
            ?? $"No transport info available for {destination}";
    }

    [Description("Looks up current pricing and availability for accommodations at a destination.")]
    [ExportAIFunction("check_accommodation")]
    public static async Task<string> CheckAccommodation(
        [Description("The travel destination city")]
        string destination,
        [Description("Budget level: budget, mid-range, or luxury")]
        string budget,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are an accommodation search API. Return:
                  - Pricing for the budget level (local currency)
                  - Availability percentage
                  - 3 recommended areas with brief descriptions
                  - One booking tip
                """),
            new(ChatRole.User,
                $"Find {budget} accommodation in {destination}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text
            ?? $"No accommodation info for {destination}";
    }
}
