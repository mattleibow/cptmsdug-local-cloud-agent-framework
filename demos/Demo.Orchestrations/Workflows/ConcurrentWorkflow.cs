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
    public static void AddConcurrentWorkflow(
        this IHostApplicationBuilder builder)
    {
        var travelTools = TravelToolContext.Default.Tools;

        builder.AddAIAgent(
            "concurrent-travel-food",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Culinary expert recommending restaurants and local dishes.",
                instructions: """
                    You are a culinary travel expert. The user will tell you a destination. Your job
                    is ONLY food recommendations — NOT a day-by-day itinerary.

                    1. Call search_restaurants for the destination
                    2. Recommend 4-6 must-try restaurants and 2-3 iconic local dishes
                    3. Include price ranges and booking tips

                    DO NOT plan a daily schedule. DO NOT mention transport, accommodation, or
                    attractions. Stay strictly in your lane. Keep to 120 words.
                    """,
                tools: [.. travelTools.Where(
                    t => t.Name == "search_restaurants")]
            ));

        builder.AddAIAgent(
            "concurrent-travel-culture",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Cultural expert recommending museums, sites, and local experiences.",
                instructions: """
                    You are a cultural travel expert. The user will tell you a destination. Your job
                    is ONLY cultural attractions — NOT a day-by-day itinerary.

                    Recommend 5-7 must-see museums, historical sites, neighbourhoods, and cultural
                    experiences for the destination. Include opening hours and one local insider tip
                    per item.

                    DO NOT plan a daily schedule. DO NOT mention restaurants, transport, or hotels.
                    Stay strictly in your lane. Keep to 120 words.
                    """,
                tools: []
            ));

        builder.AddAIAgent(
            "concurrent-travel-logistics",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Logistics expert for transport, accommodation, and routing.",
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
                    """,
                tools: [.. travelTools.Where(
                    t => t.Name is "check_transport"
                              or "check_accommodation")]
            ));

        var parallelAgents = new[]
        {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics"
        };

        builder.AddWorkflow("concurrent-travel", (sp, key) =>
        {
            var workflow = AgentWorkflowBuilder.BuildConcurrent(
                workflowName: key,
                agents: parallelAgents
                    .Select(n =>
                        sp.GetRequiredKeyedService<AIAgent>(n))
                    .ToArray(),
                aggregator: results =>
                {
                    // Combine specialist outputs into one response
                    var sections = new[]
                    {
                        "🍽️ Food & Dining",
                        "🏛️ Culture & Sites",
                        "🚗 Logistics & Routing"
                    };
                    var combined = new List<ChatMessage>();
                    var summary = new System.Text.StringBuilder();
                    summary.AppendLine("## Trip Planning Summary\n");

                    for (int i = 0;
                         i < results.Count && i < sections.Length;
                         i++)
                    {
                        var agentMessages = results[i];
                        var lastMsg = agentMessages.LastOrDefault(
                            m => m.Role == ChatRole.Assistant);
                        if (lastMsg != null)
                        {
                            summary.AppendLine($"### {sections[i]}");
                            summary.AppendLine(lastMsg.Text);
                            summary.AppendLine();
                        }
                    }

                    combined.Add(new ChatMessage(
                        ChatRole.Assistant, summary.ToString()));
                    return combined;
                });
            workflow.SetDescription(
                "Food, culture, and logistics experts plan your trip in parallel, then " +
                "results collected.");
            return workflow;
        }).AddAsAIAgent();
    }
}
