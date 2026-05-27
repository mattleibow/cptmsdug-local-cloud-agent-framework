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
// Tools used by the concurrent travel workflow
// ──────────────────────────────────────────────────────────────────────────────

public static class TravelTools
{
    [Description(
        """
        Searches for restaurants and food experiences at a destination.
        Returns top-rated options with prices.
        """)]
    [ExportAIFunction("search_restaurants")]
    public static async Task<string> SearchRestaurants(
        [Description("The travel destination city")] string destination,
        [Description("Type of cuisine (optional)")] string? cuisine = null,
        [FromServices] IChatClient chatClient = null!)
    {
        var cuisineHint = cuisine != null ? $" focusing on {cuisine} cuisine" : "";
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a restaurant search API. Return 4-5 restaurant recommendations with:
                  - Name
                  - Price range (use local currency)
                  - Rating (out of 5)
                  - Cuisine type
                Be realistic for the destination.
                """),
            new(ChatRole.User, $"Find restaurants in {destination}{cuisineHint}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text ?? $"No restaurants found in {destination}";
    }

    [Description(
        """
        Checks transport options and approximate costs between locations at the destination.
        """)]
    [ExportAIFunction("check_transport")]
    public static async Task<string> CheckTransport(
        [Description("The travel destination city")] string destination,
        [Description("Starting point within the city")] string from,
        [Description("End point within the city")] string to,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a transport information API. Return 3-4 transport options (taxi /
                rideshare, public transit, walking, rental) with estimated time, cost, and a
                local tip. Use local currency.
                """),
            new(ChatRole.User, $"Transport from {from} to {to} in {destination}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? $"No transport info available for {destination}";
    }

    [Description(
        """
        Looks up current pricing and availability for accommodations at a destination.
        """)]
    [ExportAIFunction("check_accommodation")]
    public static async Task<string> CheckAccommodation(
        [Description("The travel destination city")] string destination,
        [Description("Budget level: budget, mid-range, or luxury")] string budget,
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
            new(ChatRole.User, $"Find {budget} accommodation in {destination}")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? $"No accommodation info for {destination}";
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Concurrent travel workflow: 3 specialists fan out, aggregator concatenates
// ──────────────────────────────────────────────────────────────────────────────

public static partial class ConcurrentWorkflow
{
    [AIToolSource(typeof(TravelTools))]
    private partial class TravelToolContext : AIToolContext { }

    public static void AddConcurrentWorkflow(this IHostApplicationBuilder builder)
    {
        var travelTools = TravelToolContext.Default.Tools;

        builder.AddAIAgent("concurrent-travel-food", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Culinary expert recommending restaurants and local dishes.",
            instructions: """
                You are a culinary travel expert. The user will tell you a destination. Your job
                is ONLY food — NOT a day-by-day itinerary.

                1. Call search_restaurants for the destination
                2. Recommend 4-6 must-try restaurants and 2-3 iconic local dishes
                3. Include price ranges and booking tips

                DO NOT plan a daily schedule. DO NOT mention transport, accommodation, or
                attractions. Stay strictly in your lane. Keep to 120 words.

                FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                ## :food: Food & Dining

                **Must-try restaurants:**
                - *<Restaurant 1>* — <cuisine>, <price range>. <One-line tip.>
                - *<Restaurant 2>* — ...

                **Iconic local dishes:**
                - **<Dish 1>** — <one-line description>
                - **<Dish 2>** — ...

                **Booking tips:** <1-2 sentences>
                """,
            tools: [.. travelTools.Where(t => t.Name == "search_restaurants")]
        ).WithTelemetry());

        builder.AddAIAgent("concurrent-travel-culture", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Cultural expert recommending museums, sites, and local experiences.",
            instructions: """
                You are a cultural travel expert. The user will tell you a destination. Your job
                is ONLY cultural attractions — NOT a day-by-day itinerary.

                Recommend 5-7 must-see museums, historical sites, neighbourhoods, and cultural
                experiences. Include opening hours and one local insider tip per item.

                DO NOT plan a daily schedule. DO NOT mention restaurants, transport, or hotels.
                Stay strictly in your lane. Keep to 120 words.

                FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                ## :culture: Culture & Attractions

                **Must-see:**
                - *<Site/Museum 1>* — <hours>. <Insider tip.>
                - *<Site/Museum 2>* — ...

                **Neighbourhoods to wander:**
                - *<Neighbourhood 1>* — <vibe + tip>
                - *<Neighbourhood 2>* — ...
                """,
            tools: []
        ).WithTelemetry());

        builder.AddAIAgent("concurrent-travel-logistics", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Logistics expert for transport, accommodation, and routing.",
            instructions: """
                You are a travel logistics expert. The user will tell you a destination and trip
                duration. Your job is ONLY transport and accommodation — NOT a day-by-day
                itinerary of activities.

                1. Call check_accommodation for the destination
                2. Call check_transport for 2-3 common routes
                3. Recommend the best neighbourhoods to stay in
                4. Give total budget estimate (transport + accommodation only)

                DO NOT plan daily activities. DO NOT mention restaurants or attractions. Stay
                strictly in your lane. Keep to 120 words.

                FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                ## :transport: Logistics & Stay

                **Where to stay:**
                - *<Area 1>* — <price/night>. <Why.>
                - *<Area 2>* — ...

                **Getting around:**
                - *<Route or Mode 1>* — <cost>. <Note.>
                - *<Route or Mode 2>* — ...

                **Estimated total budget (transport + accommodation):** <range>
                """,
            tools: [.. travelTools.Where(t =>
                t.Name is "check_transport" or "check_accommodation")]
        ).WithTelemetry());

        var parallelAgents = new[]
        {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics",
        };

        builder.AddWorkflow("concurrent-travel", (sp, key) =>
        {
            return AgentWorkflowBuilder.BuildConcurrent(
                workflowName: key,
                agents: parallelAgents
                    .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                    .ToArray(),
                aggregator: results =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("# :map: Trip Plan");
                    sb.AppendLine();
                    foreach (var agentMessages in results)
                    {
                        var last = agentMessages.LastOrDefault(m => m.Role == ChatRole.Assistant);
                        if (last is null) continue;
                        sb.AppendLine(last.Text);
                        sb.AppendLine();
                    }
                    return [new ChatMessage(ChatRole.Assistant, sb.ToString())];
                })
                .SetDescription(
                    "Food, culture, and logistics experts fan out concurrently. Each " +
                    "returns a self-contained Markdown section, then the aggregator " +
                    "stitches them into one trip plan.");
        }).AddAsAIAgent();
    }
}
