using System.ComponentModel;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Tools exposed to the cloud invite-drafter agent (stage 3).
///
/// <see cref="GetCalendar"/> is the demo's headline local tool: the cloud
/// agent calls it to find a free slot in the user's calendar. The cloud
/// never sees the raw events — just the free/busy view the tool returns.
/// This is something a server genuinely cannot answer; the user's
/// calendar lives on the device.
/// </summary>
public static class LocalCalendarTool
{
    [Description(
        """
        Returns a Markdown free/busy view of the user's local calendar
        for the given day or range. Use a natural-language date like
        "tomorrow", "Tuesday", or "this week". Call this once before
        proposing a meeting time so you suggest a slot the user is
        actually free.
        """)]
    [ExportAIFunction("get_calendar")]
    public static Task<string> GetCalendar(
        string dateOrRange,
        [FromServices] CalendarService calendar,
        CancellationToken cancellationToken = default)
        => calendar.GetCalendarAsync(dateOrRange, cancellationToken);
}
