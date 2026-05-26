using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations;

public static class ConcurrentWorkflow
{
    public static void AddConcurrentWorkflow(this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: "concurrent-travel-food",
            instructions: """
                You are a culinary travel expert. The user will tell you a destination.
                Recommend the best restaurants, food experiences, and local dishes for THAT
                specific destination. Include price range and booking tips. Keep to 150 words.
                """,
            description: "Culinary expert recommending restaurants and local dishes.",
            chatClientServiceKey: null,
            lifetime: ServiceLifetime.Transient);

        builder.AddAIAgent(
            name: "concurrent-travel-culture",
            instructions: """
                You are a cultural travel expert. The user will tell you a destination.
                Recommend museums, temples, historical sites, and local experiences for THAT
                specific destination. Include opening hours and tips. Keep to 150 words.
                """,
            description: "Cultural expert recommending museums, sites, and local experiences.",
            chatClientServiceKey: null,
            lifetime: ServiceLifetime.Transient);

        builder.AddAIAgent(
            name: "concurrent-travel-logistics",
            instructions: """
                You are a travel logistics expert. The user will tell you a destination and duration.
                Recommend transportation, accommodation areas, and day-by-day routing for THAT
                specific destination. Include budget estimates. Keep to 150 words.
                """,
            description: "Logistics expert for transport, accommodation, and routing.",
            chatClientServiceKey: null,
            lifetime: ServiceLifetime.Transient);

        builder.AddAIAgent(
            name: "concurrent-travel-coordinator",
            instructions: """
                You are a trip coordinator. Take multiple specialist recommendations and weave them
                into a cohesive day-by-day itinerary. Resolve conflicts and balance the schedule.
                Keep to 250 words.
                """,
            description: "Trip coordinator that synthesizes specialist input into a cohesive plan.",
            chatClientServiceKey: null,
            lifetime: ServiceLifetime.Transient);

        var parallelAgents = new[] {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics"
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
                    var combined = results.SelectMany(r => r).ToList();
                    combined.Add(new ChatMessage(
                        ChatRole.User,
                        "Synthesize the above specialist recommendations into a cohesive plan."));
                    return combined;
                });
            workflow.SetDescription("Food, culture, and logistics experts plan in parallel, then a coordinator merges results.");
            return workflow;
        }).AddAsAIAgent();
    }
}
