using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Demo.Orchestrations.Tools;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

public static class HandoffWorkflow
{
    public static void AddHandoffWorkflow(
        this IHostApplicationBuilder builder)
    {
        var helpTools = HelpDeskToolContext.Default.Tools;

        // The dispatcher must invoke a handoff_to_<agent>
        // function rather than replying with free-form text —
        // that is how the handoff workflow routes. It also
        // provides a brief acknowledgment so the user sees
        // something in the chat.
        builder.AddAIAgent(
            name: "handoff-helpdesk-dispatcher",
            instructions: """
                You are an IT help desk triage assistant. Read the user's issue, provide a ONE
                sentence acknowledgment (e.g., "I'll connect you with our network specialist for
                this VPN issue."), then immediately route the conversation to the correct specialist
                by invoking the matching handoff_to_* function. Do not try to solve the problem
                yourself and do not ask clarifying questions.
                """,
            description:
                "IT help desk dispatcher that routes incoming issues to the right specialist.",
            chatClientServiceKey: null);

        // Network specialist with KB search and system status
        builder.AddAIAgent(
            "handoff-helpdesk-network",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Network specialist. Handles VPN, Wi-Fi, connectivity, firewall, and DNS " +
                    "issues.",
                instructions: """
                    You are a network support specialist. Use the search_knowledge_base tool to
                    find relevant solutions, and check_system_status to verify if there are known
                    outages. If you cannot resolve the issue, use create_ticket to escalate. Keep
                    responses under 200 words.

                    FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                    ## :network: Network specialist

                    **Diagnosis:** <one-line summary of likely cause>

                    **Try these steps:**
                    - <Step 1>
                    - <Step 2>
                    - <Step 3>

                    **System status:** <one line — operational, degraded, or outage details>
                    """,
                tools: [.. helpTools.Where(t =>
                    t.Name is "search_knowledge_base"
                           or "check_system_status"
                           or "create_ticket")]
            ));

        // Software specialist with KB search and ticket tools
        builder.AddAIAgent(
            "handoff-helpdesk-software",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Software specialist. Handles application crashes, installation, updates, " +
                    "and licensing.",
                instructions: """
                    You are a software support specialist. Use search_knowledge_base to find known
                    fixes for application issues. Help with crashes, installation problems, and
                    updates. Create a ticket with create_ticket if the issue requires further
                    investigation. Keep responses under 200 words.

                    FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                    ## :bug: Software specialist

                    **Diagnosis:** <one-line summary of likely cause>

                    **Try these steps:**
                    - <Step 1>
                    - <Step 2>
                    - <Step 3>

                    **Ticket:** <ticket ID if created, otherwise "Not needed yet">
                    """,
                tools: [.. helpTools.Where(t =>
                    t.Name is "search_knowledge_base"
                           or "create_ticket")]
            ));

        // Hardware specialist with KB search and ticket tools
        builder.AddAIAgent(
            "handoff-helpdesk-hardware",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Hardware specialist. Handles laptop, monitor, peripheral, and " +
                    "docking-station problems.",
                instructions: """
                    You are a hardware support specialist. Use search_knowledge_base to find
                    diagnostic steps for hardware issues. Diagnose laptop, monitor, peripheral,
                    and docking station problems. If RMA is needed, use create_ticket to initiate
                    the process. Keep responses under 200 words.

                    FORMAT YOUR RESPONSE EXACTLY LIKE THIS (Markdown):

                    ## :wrench: Hardware specialist

                    **Diagnosis:** <one-line summary of likely cause>

                    **Try these steps:**
                    - <Step 1>
                    - <Step 2>
                    - <Step 3>

                    **Ticket / RMA:** <ticket ID if created, otherwise "Not needed yet">
                    """,
                tools: [.. helpTools.Where(t =>
                    t.Name is "search_knowledge_base"
                           or "create_ticket")]
            ));

        builder.AddWorkflow("handoff-helpdesk", (sp, key) =>
        {
            var dispatcher = sp.GetRequiredKeyedService<AIAgent>(
                "handoff-helpdesk-dispatcher");
            var network = sp.GetRequiredKeyedService<AIAgent>(
                "handoff-helpdesk-network");
            var software = sp.GetRequiredKeyedService<AIAgent>(
                "handoff-helpdesk-software");
            var hardware = sp.GetRequiredKeyedService<AIAgent>(
                "handoff-helpdesk-hardware");

            var workflow = AgentWorkflowBuilder
                .CreateHandoffBuilderWith(dispatcher)
                .WithHandoffs(dispatcher,
                    [network, software, hardware])
                .WithHandoffs(
                    [network, software, hardware], dispatcher)
                .EmitAgentResponseEvents()
                .Build();

            typeof(Workflow)
                .GetProperty(nameof(Workflow.Name))!
                .SetValue(workflow, key);
            workflow.SetDescription(
                "Dispatcher triages issues and hands off to network, software, or hardware " +
                "specialists.");
            return workflow;
        }).AddAsAIAgent();
    }
}
