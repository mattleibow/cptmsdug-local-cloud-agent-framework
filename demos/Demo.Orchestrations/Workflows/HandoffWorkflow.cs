using Microsoft.Extensions.Hosting;
using System.Reflection;
using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

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

        // Register workflow directly (HandoffWorkflowBuilder.Build() doesn't support naming via fluent API)
        builder.Services.AddKeyedSingleton<Workflow>(def.Id, (sp, _) =>
        {
            var agents = def.Agents
                .Select(a => sp.GetRequiredKeyedService<AIAgent>(a.Name))
                .ToArray();

            var dispatcher = agents[0];
            var specialists = agents.Skip(1).ToArray();

            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher, specialists)
                .Build();

            // Set the Name via reflection (init-only property, but the backing field is settable)
            typeof(Workflow).GetProperty(nameof(Workflow.Name))!
                .SetValue(workflow, def.Id);

            return workflow;
        });

        // Also expose as AIAgent for the DevUI entity discovery
        builder.Services.AddKeyedSingleton<AIAgent>(def.Id, (sp, _) =>
            sp.GetRequiredKeyedService<Workflow>(def.Id).AsAIAgent(name: def.Id));
    }
}
