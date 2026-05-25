using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

public static class HandoffWorkflow
{
    public static void AddHandoffWorkflow(this IHostApplicationBuilder builder)
    {
        var def = DemoWorkflows.Handoff;

        foreach (var agent in def.Agents)
        {
            builder.AddAIAgent(agent.Name, agent.SystemPrompt);
        }

        // HandoffWorkflowBuilder doesn't yet support WithName (MAF API gap),
        // so we register the workflow directly as a keyed singleton.
        builder.Services.AddKeyedSingleton<Workflow>(def.Id, (sp, _) =>
        {
            var agents = def.Agents
                .Select(a => sp.GetRequiredKeyedService<AIAgent>(a.Name))
                .ToArray();

            var dispatcher = agents[0];
            var specialists = agents.Skip(1).ToArray();

            return AgentWorkflowBuilder.CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher, specialists)
                .Build();
        });

        builder.Services.AddKeyedSingleton<AIAgent>(def.Id, (sp, _) =>
            sp.GetRequiredKeyedService<Workflow>(def.Id).AsAIAgent(name: def.Id));
    }
}
