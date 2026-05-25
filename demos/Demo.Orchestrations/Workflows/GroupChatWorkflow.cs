using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

public static class GroupChatWorkflow
{
    public static void AddGroupChatWorkflow(this IHostApplicationBuilder builder)
    {
        var def = DemoWorkflows.GroupChat;

        foreach (var agent in def.Agents)
        {
            builder.AddAIAgent(agent.Name, agent.SystemPrompt);
        }

        builder.AddWorkflow(def.Id, (sp, key) =>
        {
            var participants = def.Agents
                .Select(a => sp.GetRequiredKeyedService<AIAgent>(a.Name))
                .ToArray();

            return AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 3 })
                .AddParticipants(participants)
                .WithName(key)
                .Build();
        }).AddAsAIAgent(def.Id);
    }
}
