using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the helpdesk specialist agents.
/// </summary>
public static class HelpDeskTools
{
    [Description("Searches the internal IT knowledge base for solutions to common " +
        "problems. Returns matching articles.")]
    [ExportAIFunction("search_knowledge_base")]
    public static async Task<string> SearchKnowledgeBase(
        string issue,
        [FromServices] IChatClient chatClient)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new(ChatRole.System, """
                You are an IT knowledge base search engine.
                Return a single KB article with:
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

    [Description("Creates a support ticket in the ticketing system. Returns the ticket " +
        "ID for tracking.")]
    [ExportAIFunction("create_ticket")]
    public static string CreateTicket(
        string summary, string priority, string assignedTeam)
    {
        var ticketId = $"INC{Random.Shared.Next(100000, 999999)}";
        var sla = priority == "High" ? "4 hours"
            : priority == "Medium" ? "8 hours"
            : "24 hours";
        return $"""
            ✓ Ticket Created Successfully
            ─────────────────────────────
            Ticket ID:    {ticketId}
            Summary:      {summary}
            Priority:     {priority}
            Assigned To:  {assignedTeam}
            Status:       Open
            SLA Target:   {sla}
            ─────────────────────────────
            Track at: https://helpdesk.internal/track/{ticketId}
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
                You are a system status dashboard. Generate a realistic IT status report showing 5-7
                services (Email, VPN, Teams, SharePoint, Azure DevOps, etc.) with most
                operational (✓)
                and 1-2 degraded (⚠) or down (✗). Include a timestamp. Be concise.
                """),
            new(ChatRole.User, "Show current system status")
        ],
        new() { MaxOutputTokens = 200 });
        return response.Text ?? "System status unavailable";
    }
}
