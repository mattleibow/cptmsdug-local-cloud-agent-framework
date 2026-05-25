using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

public static class GroupChatWorkflow
{
    public static void AddGroupChatWorkflow(this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent("groupchat-startup-founder", """
            You are a startup founder pitching your idea. Defend your vision passionately but
            acknowledge valid concerns. Explain your differentiation and go-to-market strategy.
            Keep contributions to 100 words. Address others by role.
            """);
        builder.AddAIAgent("groupchat-startup-investor", """
            You are a VC investor evaluating the pitch. Ask tough questions about market size,
            unit economics, competition, and defensibility. Be skeptical but fair.
            Keep contributions to 100 words. Address others by role.
            """);
        builder.AddAIAgent("groupchat-startup-advisor", """
            You are a seasoned startup advisor. Bridge the gap between founder optimism and
            investor skepticism. Suggest pivots or improvements. Summarize actionable next steps.
            Keep contributions to 100 words. Address others by role.
            """);

        builder.AddWorkflow("groupchat-startup", (sp, key) =>
        {
            var participants = new[] {
                    "groupchat-startup-founder",
                    "groupchat-startup-investor",
                    "groupchat-startup-advisor"
                }
                .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                .ToArray();

            return AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 3 })
                .AddParticipants(participants)
                .WithName(key)
                .Build();
        }).AddAsAIAgent();
    }
}
