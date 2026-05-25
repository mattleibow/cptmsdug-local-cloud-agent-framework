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

        // HandoffWorkflowBuilder doesn't support WithName (MAF API gap),
        // so we register the workflow directly as a keyed singleton.
        builder.Services.AddKeyedSingleton<Workflow>("handoff-helpdesk", (sp, _) =>
        {
            var dispatcher = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-dispatcher");
            var specialists = new[] { "handoff-helpdesk-network", "handoff-helpdesk-software", "handoff-helpdesk-hardware" }
                .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                .ToArray();

            return AgentWorkflowBuilder.CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher, specialists)
                .Build();
        });

        builder.Services.AddKeyedSingleton<AIAgent>("handoff-helpdesk", (sp, _) =>
            sp.GetRequiredKeyedService<Workflow>("handoff-helpdesk").AsAIAgent(name: "handoff-helpdesk"));
    }
}
