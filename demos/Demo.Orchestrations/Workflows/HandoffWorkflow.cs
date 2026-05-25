using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

public static class HandoffWorkflow
{
    public static void AddHandoffWorkflow(this IHostApplicationBuilder builder)
    {
        // The dispatcher must invoke a handoff_to_<agent> function rather than
        // replying with free-form text — that is how the handoff workflow routes.
        builder.AddAIAgent(
            name: "handoff-helpdesk-dispatcher",
            instructions: """
                You are an IT help desk triage assistant. Read the user's issue and immediately
                route the conversation to the correct specialist by invoking the matching
                handoff_to_* function. Do not try to solve the problem yourself, do not ask
                clarifying questions, and do not narrate the handoff to the user — just call
                the appropriate handoff function.
                """,
            description: "IT help desk dispatcher that routes incoming issues to the right specialist.",
            chatClientServiceKey: null);

        // The Description on each specialist becomes the description of the
        // generated handoff_to_<n> tool, which is what the dispatcher uses to
        // pick the right target. Make it specific.
        builder.AddAIAgent(
            name: "handoff-helpdesk-network",
            instructions: """
                You are a network support specialist. Troubleshoot VPN, Wi-Fi, DNS, firewall, and
                connectivity issues. Provide step-by-step diagnostic instructions. Ask clarifying
                questions if needed. Keep responses under 200 words.
                """,
            description: "Network specialist. Handles VPN, Wi-Fi, connectivity, firewall, and DNS issues.",
            chatClientServiceKey: null);

        builder.AddAIAgent(
            name: "handoff-helpdesk-software",
            instructions: """
                You are a software support specialist. Help with application crashes, installation
                problems, update failures, and licensing. Provide clear fix steps. Keep responses
                under 200 words.
                """,
            description: "Software specialist. Handles application crashes, installation, updates, and licensing.",
            chatClientServiceKey: null);

        builder.AddAIAgent(
            name: "handoff-helpdesk-hardware",
            instructions: """
                You are a hardware support specialist. Diagnose laptop, monitor, peripheral, and
                docking station problems. Determine if RMA is needed. Keep responses under 200 words.
                """,
            description: "Hardware specialist. Handles laptop, monitor, peripheral, and docking-station problems.",
            chatClientServiceKey: null);

        builder.AddWorkflow("handoff-helpdesk", (sp, key) =>
        {
            var dispatcher = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-dispatcher");
            var network = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-network");
            var software = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-software");
            var hardware = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-hardware");

            return AgentWorkflowBuilder.CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher, [network, software, hardware])
                .WithHandoffs([network, software, hardware], dispatcher)
                .EmitAgentResponseEvents()
                .Build();
        }).AddAsAIAgent();
    }
}
