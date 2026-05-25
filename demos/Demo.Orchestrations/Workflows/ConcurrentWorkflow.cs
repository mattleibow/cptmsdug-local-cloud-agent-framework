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
        builder.AddAIAgent("concurrent-travel-food", """
            You are a culinary travel expert. Recommend restaurants, food experiences, and local
            dishes for the destination. Include price range and booking tips. Keep to 150 words.
            """);
        builder.AddAIAgent("concurrent-travel-culture", """
            You are a cultural travel expert. Recommend museums, temples, historical sites, and
            local experiences. Include opening hours and tips. Keep to 150 words.
            """);
        builder.AddAIAgent("concurrent-travel-logistics", """
            You are a travel logistics expert. Recommend transportation, accommodation areas,
            and day-by-day routing for efficiency. Include budget estimates. Keep to 150 words.
            """);
        builder.AddAIAgent("concurrent-travel-coordinator", """
            You are a trip coordinator. Take multiple specialist recommendations and weave them
            into a cohesive day-by-day itinerary. Resolve conflicts and balance the schedule.
            Keep to 250 words.
            """);

        var parallelAgents = new[] { "concurrent-travel-food", "concurrent-travel-culture", "concurrent-travel-logistics" };

        builder.AddWorkflow("concurrent-travel", (sp, key) => AgentWorkflowBuilder.BuildConcurrent(
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
            }
        )).AddAsAIAgent();
    }
}
