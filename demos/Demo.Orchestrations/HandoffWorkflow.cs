using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

#pragma warning disable MAAIW001 // Experimental API

namespace Demo.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Tools used by the handoff helpdesk workflow
// ──────────────────────────────────────────────────────────────────────────────

public static class HelpDeskTools
{
    [Description("Searches the internal IT knowledge base for solutions to common problems. Returns matching articles.")]
    [ExportAIFunction("search_knowledge_base")]
    public static async Task<string> SearchKnowledgeBase(
        string issue,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are an IT knowledge base search engine. Return a single KB article with:
                  - A ticket number in KB-XXXX format
                  - 4-5 step-by-step troubleshooting instructions
                  - A resolution rate percentage
                Be specific and realistic.
                """),
            new(ChatRole.User, $"Search KB for: {issue}")
        ],
        new() { MaxOutputTokens = 250 });
        return response.Text ?? $"KB-0000: No match found for \"{issue}\"";
    }

    [Description("Creates a support ticket in the ticketing system. Returns the ticket ID for tracking.")]
    [ExportAIFunction("create_ticket")]
    public static string CreateTicket(string summary, string priority, string assignedTeam)
    {
        var ticketId = $"INC{Random.Shared.Next(100000, 999999)}";
        var sla = priority == "High" ? "4 hours"
            : priority == "Medium" ? "8 hours"
            : "24 hours";
        return $"""
            :check: Ticket created
            Ticket ID:   {ticketId}
            Summary:     {summary}
            Priority:    {priority}
            Assigned:    {assignedTeam}
            SLA Target:  {sla}
            Track at:    https://helpdesk.internal/track/{ticketId}
            """;
    }

    [Description("Checks the current system status for known outages or maintenance windows.")]
    [ExportAIFunction("check_system_status")]
    public static async Task<string> CheckSystemStatus(
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are a system status dashboard. Generate a realistic IT status report showing
                5-7 services (Email, VPN, Teams, SharePoint, Azure DevOps, etc.) with most
                operational and 1-2 degraded or down. Include a timestamp. Be concise.
                """),
            new(ChatRole.User, "Show current system status")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? "System status unavailable";
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Handoff helpdesk workflow: Dispatcher → Network / Software / Hardware
// ──────────────────────────────────────────────────────────────────────────────

public static partial class HandoffWorkflow
{
    [AIToolSource(typeof(HelpDeskTools))]
    private partial class HelpDeskToolContext : AIToolContext { }

    public static void AddHandoffWorkflow(this IHostApplicationBuilder builder)
    {
        var helpTools = HelpDeskToolContext.Default.Tools;

        // The dispatcher must invoke a handoff_to_<agent> function rather than replying
        // with free-form text — that is how the handoff workflow routes. It also provides
        // a brief acknowledgment so the user sees something in the chat.
        builder.AddAIAgent(
            name: "handoff-helpdesk-dispatcher",
            instructions: """
                You are an IT help desk triage assistant. Your job has TWO MANDATORY parts and
                you MUST do BOTH every single time:

                **STEP 1 (always first):** Output a one-sentence acknowledgment as visible text.
                FORMAT EXACTLY: ":transport: **Dispatcher:** I'll connect you with our
                <specialist> for this <topic> issue."

                **STEP 2 (only after step 1):** Invoke the matching handoff_to_* function to
                route the conversation to the correct specialist (network / software / hardware).

                Do NOT skip step 1. Do NOT call the handoff function without text first. Do NOT
                try to solve the problem yourself. Do NOT ask clarifying questions.
                """,
            description:
                "IT help desk dispatcher that routes incoming issues to the right specialist.",
            chatClientServiceKey: null);

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
                    t.Name is "search_knowledge_base" or "check_system_status" or "create_ticket")]
            ));

        builder.AddAIAgent(
            "handoff-helpdesk-software",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Software specialist. Handles application crashes, installation, updates, " +
                    "and licensing.",
                instructions: """
                    You are a software support specialist. Use search_knowledge_base to find
                    known fixes for application issues. Help with crashes, installation problems,
                    and updates. Create a ticket with create_ticket if the issue requires further
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
                    t.Name is "search_knowledge_base" or "create_ticket")]
            ));

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
                    t.Name is "search_knowledge_base" or "create_ticket")]
            ));

        builder.AddWorkflow("handoff-helpdesk", (sp, key) =>
        {
            var dispatcher = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-dispatcher");
            var network = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-network");
            var software = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-software");
            var hardware = sp.GetRequiredKeyedService<AIAgent>("handoff-helpdesk-hardware");

            return AgentWorkflowBuilder
                .CreateHandoffBuilderWith(dispatcher)
                .WithName(key)
                .WithDescription(
                    "Dispatcher triages issues and hands off to network, software, or " +
                    "hardware specialists.")
                .WithHandoffs(dispatcher, [network, software, hardware])
                .WithHandoffs([network, software, hardware], dispatcher)
                .EmitAgentResponseEvents()
                .Build();
        }).AddAsAIAgent();
    }
}
