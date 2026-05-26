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
        builder.AddAIAgent(
            name: "groupchat-startup-founder",
            instructions: """
                You are a startup founder in a pitch meeting. On your FIRST turn, briefly introduce your
                startup idea. On SUBSEQUENT turns, respond directly to what the investor or advisor just said —
                defend criticisms, answer questions, incorporate feedback, and refine your pitch.
                Keep each contribution to 80 words. Address others by role (Investor, Advisor).
                Never repeat your introduction. Build on the conversation.
                """,
            description: "Startup founder pitching and defending their vision.",
            chatClientServiceKey: null);

        builder.AddAIAgent(
            name: "groupchat-startup-investor",
            instructions: """
                You are a VC investor in a pitch meeting. On your FIRST turn, react to the founder's pitch
                with an initial assessment. On SUBSEQUENT turns, follow up on previous answers — dig deeper
                into weak points, acknowledge good responses, and raise NEW concerns you haven't mentioned yet.
                Keep contributions to 80 words. Address others by role (Founder, Advisor).
                Never repeat previous questions.
                """,
            description: "VC investor evaluating the pitch with tough questions.",
            chatClientServiceKey: null);

        builder.AddAIAgent(
            name: "groupchat-startup-advisor",
            instructions: """
                You are a seasoned startup advisor in a pitch meeting. On your FIRST turn, share initial
                thoughts. On SUBSEQUENT turns, mediate between the founder and investor — acknowledge valid
                points from both, suggest compromises or pivots, and on your final turn provide a brief
                summary of actionable next steps.
                Keep contributions to 80 words. Address others by role (Founder, Investor).
                """,
            description: "Seasoned advisor bridging optimism and skepticism.",
            chatClientServiceKey: null);

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
                .WithDescription("Founder pitches, investor challenges, advisor mediates — 3 rounds.")
                .Build();
        }).AddAsAIAgent();
    }
}
