using System.ComponentModel;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the helpdesk specialist agents.
/// </summary>
public static class HelpDeskTools
{
    [Description("Searches the internal IT knowledge base for solutions to common problems. Returns matching articles.")]
    [ExportAIFunction("search_knowledge_base")]
    public static string SearchKnowledgeBase(string issue)
    {
        // Simulated KB search for demo
        var lower = issue.ToLowerInvariant();
        if (lower.Contains("vpn") || lower.Contains("connect"))
            return """
                KB-2041: VPN Connection Troubleshooting
                - Step 1: Verify network connectivity (ping 8.8.8.8)
                - Step 2: Clear VPN client cache (Settings > Advanced > Clear Cache)
                - Step 3: Re-authenticate with corporate credentials
                - Step 4: If still failing, check if split-tunneling is enabled
                Resolution rate: 87%
                """;
        if (lower.Contains("keyboard") || lower.Contains("peripheral") || lower.Contains("usb"))
            return """
                KB-1089: Keyboard/Peripheral Not Responding
                - Step 1: Try a different USB port
                - Step 2: Check Device Manager for driver issues (⚠️ icon)
                - Step 3: For wireless: replace batteries, re-pair via Bluetooth settings
                - Step 4: Test with another known-good keyboard
                - If none work: likely USB controller failure → escalate to hardware RMA
                Resolution rate: 92%
                """;
        if (lower.Contains("crash") || lower.Contains("freeze") || lower.Contains("hang"))
            return """
                KB-3015: Application Crash/Freeze Recovery
                - Step 1: Force-quit the application (Ctrl+Alt+Del or Cmd+Opt+Esc)
                - Step 2: Clear application cache/temp files
                - Step 3: Check for pending updates
                - Step 4: Run as administrator/with elevated permissions
                - Step 5: Collect crash dump and submit to vendor support
                Resolution rate: 78%
                """;
        if (lower.Contains("screen") || lower.Contains("display") || lower.Contains("flicker") || lower.Contains("monitor"))
            return """
                KB-1523: Display/Screen Issues
                - Step 1: Check cable connections (reseat HDMI/DisplayPort/USB-C)
                - Step 2: Try external monitor to isolate laptop panel vs GPU
                - Step 3: Update GPU drivers (Intel/NVIDIA/AMD)
                - Step 4: Check display refresh rate (Settings > Display > Advanced)
                - Step 5: If laptop panel flickering persists → likely cable or panel failure → RMA
                Resolution rate: 71%
                """;
        return $"""
            KB-0000: No exact match found for "{issue}"
            Suggested actions:
            - Ask the user for more details about the symptoms
            - Check if the issue is reproducible
            - Escalate to Tier 2 if unresolvable within 15 minutes
            """;
    }

    [Description("Creates a support ticket in the ticketing system. Returns the ticket ID for tracking.")]
    [ExportAIFunction("create_ticket")]
    public static string CreateTicket(string summary, string priority, string assignedTeam)
    {
        var ticketId = $"INC{Random.Shared.Next(100000, 999999)}";
        return $"""
            ✓ Ticket Created Successfully
            ─────────────────────────────
            Ticket ID:    {ticketId}
            Summary:      {summary}
            Priority:     {priority}
            Assigned To:  {assignedTeam}
            Status:       Open
            SLA Target:   {(priority == "High" ? "4 hours" : priority == "Medium" ? "8 hours" : "24 hours")}
            ─────────────────────────────
            The user can track this ticket at: https://helpdesk.internal/track/{ticketId}
            """;
    }

    [Description("Checks the current system status for known outages or maintenance windows.")]
    [ExportAIFunction("check_system_status")]
    public static string CheckSystemStatus()
    {
        return """
            System Status Dashboard (Last updated: 2 minutes ago)
            ─────────────────────────────────────────────────────
            ✓ Email (Exchange Online)     : Operational
            ✓ VPN (GlobalProtect)         : Operational
            ⚠ SharePoint Online           : Degraded Performance (since 14:30 UTC)
            ✓ Teams                       : Operational
            ✓ Azure DevOps                : Operational
            ✓ Internal Wiki               : Operational
            
            Active Maintenance: None scheduled for next 24 hours
            """;
    }
}
