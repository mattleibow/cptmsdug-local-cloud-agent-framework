using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Demo1.BasicAgent.Workflows;

public static class ConcurrentWorkflow
{
    public static void AddConcurrentWorkflow(this IHostApplicationBuilder builder)
    {
        var def = DemoWorkflows.Concurrent;

        foreach (var agent in def.Agents)
        {
            builder.AddAIAgent(agent.Name, agent.SystemPrompt);
        }

        // All agents except the last one (coordinator) run in parallel
        var parallelAgentNames = def.Agents.SkipLast(1).Select(a => a.Name).ToArray();

        builder.AddWorkflow(def.Id, (sp, key) => AgentWorkflowBuilder.BuildConcurrent(
            workflowName: key,
            agents: parallelAgentNames
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
        )).AddAsAIAgent(def.Id);
    }
}
