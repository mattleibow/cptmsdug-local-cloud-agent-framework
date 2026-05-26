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
                    You are a culinary travel expert. The user will tell you a destination. Use the
                    search_restaurants tool to find top dining options, then recommend the best food
                    experiences and local dishes for THAT specific destination. Include price range
                    and booking tips. Keep to 150 words.
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
                    You are a cultural travel expert. The user will tell you a destination.
                    Recommend
                    museums, temples, historical sites, and local experiences for THAT specific
                    destination. Include opening hours and tips. Keep to 150 words.
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
                    You are a travel logistics expert. The user will tell you a destination and
                    duration. Use the check_transport and check_accommodation tools to get real
                    pricing data, then recommend transportation, accommodation areas, and day-by-day
                    routing for THAT specific destination. Include budget estimates. Keep
                    to 150 words.
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
