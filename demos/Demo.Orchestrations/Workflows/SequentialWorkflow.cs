using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

namespace Demo.Orchestrations;

public static class SequentialWorkflow
{
    public static void AddSequentialWorkflow(this IHostApplicationBuilder builder)
    {
        var def = DemoWorkflows.Sequential;

        foreach (var agent in def.Agents)
        {
            builder.AddAIAgent(agent.Name, agent.SystemPrompt);
        }

        builder.AddWorkflow(def.Id, (sp, key) => AgentWorkflowBuilder.BuildSequential(
            workflowName: key,
            agents: def.Agents
                .Select(a => sp.GetRequiredKeyedService<AIAgent>(a.Name))
                .ToArray()
        )).AddAsAIAgent(def.Id);
    }
}
