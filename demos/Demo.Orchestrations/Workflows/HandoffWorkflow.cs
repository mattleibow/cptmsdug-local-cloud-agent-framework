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
        builder.AddAIAgent("handoff-helpdesk-dispatcher", """
            You are an IT help desk dispatcher. Analyze the user's issue and route to the
            correct specialist. Available specialists and their domains:
            - handoff-helpdesk-network: VPN, Wi-Fi, connectivity, firewall, DNS issues
            - handoff-helpdesk-software: App crashes, installation, updates, licensing
            - handoff-helpdesk-hardware: Laptop, monitor, peripherals, docking station issues
            Route by responding with the specialist name and a brief reason.
            """);
        builder.AddAIAgent("handoff-helpdesk-network", """
            You are a network support specialist. Troubleshoot VPN, Wi-Fi, DNS, firewall, and
            connectivity issues. Provide step-by-step diagnostic instructions. Ask clarifying
            questions if needed. Keep responses under 200 words.
            """);
        builder.AddAIAgent("handoff-helpdesk-software", """
            You are a software support specialist. Help with application crashes, installation
            problems, update failures, and licensing. Provide clear fix steps. Keep responses
            under 200 words.
            """);
        builder.AddAIAgent("handoff-helpdesk-hardware", """
            You are a hardware support specialist. Diagnose laptop, monitor, peripheral, and
            docking station problems. Determine if RMA is needed. Keep responses under 200 words.
            """);

        // Register the workflow. The HandoffWorkflowBuilder creates an internal
        // "HandoffStart" entry point. We name the workflow and expose it as an AIAgent.
        builder.Services.AddKeyedSingleton<Workflow>("handoff-helpdesk", (sp, _) =>
        {
            var dispatcher = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-dispatcher");
            var network = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-network");
            var software = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-software");
            var hardware = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-hardware");

            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher, [network, software, hardware])
                .WithHandoffs([network, software, hardware], dispatcher)
                .Build();

            return workflow;
        });

        // Also register the "HandoffStart" pseudo-agent to satisfy the workflow entry point
        builder.Services.AddKeyedSingleton<AIAgent>("HandoffStart", (sp, _) =>
            sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-dispatcher"));

        builder.Services.AddKeyedSingleton<AIAgent>("handoff-helpdesk", (sp, _) =>
            sp.GetRequiredKeyedService<Workflow>("handoff-helpdesk").AsAIAgent(name: "handoff-helpdesk"));
    }
}
