using System.ComponentModel;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Tools exposed to the cloud invite-drafter agent (stage 5).
///
/// <see cref="GetCalendar"/> is the demo's headline local tool: the cloud
/// agent calls it to plan a meeting time. The cloud never sees the raw
/// events — just an opaque view: the user's working hours and a list of
/// busy time ranges, no titles. This is something a server genuinely
/// cannot answer; the user's calendar lives on the device.
/// </summary>
public static class LocalCalendarTool
{
    [Description(
        """
        Returns the user's working hours and an OPAQUE list of busy time
        ranges for the given day or range. Use a natural-language date
        like "tomorrow", "Tuesday", or "this week". The tool intentionally
        does not reveal event titles, attendees, or projects — only when
        the user is busy. Call this once before proposing a meeting time
        so you can suggest a slot inside working hours that does not
        overlap any busy block.
        """)]
    [ExportAIFunction("get_calendar")]
    public static Task<string> GetCalendar(
        string dateOrRange,
        [FromServices] CalendarService calendar,
        CancellationToken cancellationToken = default)
        => calendar.GetCalendarAsync(dateOrRange, cancellationToken);
}
