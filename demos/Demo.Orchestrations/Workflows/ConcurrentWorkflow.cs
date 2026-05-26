using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Demo.Orchestrations.Tools;

namespace Demo.Orchestrations;

public static class ConcurrentWorkflow
{
    public static void AddConcurrentWorkflow(this IHostApplicationBuilder builder)
    {
        var travelTools = TravelToolContext.Default.Tools;

        // Each specialist produces a self-contained Markdown section (## heading + body).
        // The aggregator just concatenates them — no synthesizer needed.

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
        ));

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
        ));

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
        ));

        // ── Outer workflow: BuildConcurrent with a plain text aggregator ──
        //
        // No synthesizer agent needed. Each specialist already emits a clean
        // Markdown section, so the aggregator just stitches them together
        // under a single "# Trip Plan" heading.

        var parallelAgents = new[]
        {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics",
        };

        builder.AddWorkflow("concurrent-travel", (sp, key) =>
        {
            var workflow = AgentWorkflowBuilder.BuildConcurrent(
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
                });
            workflow.SetDescription(
                "Food, culture, and logistics experts fan out concurrently. Each returns a " +
                "self-contained Markdown section, then the aggregator stitches them into one " +
                "trip plan.");
            return workflow;
        }).AddAsAIAgent();
    }
}
