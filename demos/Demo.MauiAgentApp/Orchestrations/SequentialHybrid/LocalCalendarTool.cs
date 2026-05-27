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
        Returns the user's calendar for a single day: a fixed working
        window (09:00–17:00) and an OPAQUE list of busy time ranges.
        Busy ranges have no event titles, attendees, or project names —
        only when the user is busy. Working hours never change.

        Call this once before proposing a meeting time. If every gap
        inside working hours is taken (the day is fully booked), call
        the tool again with a later date until you find a day with a
        usable gap, then propose a time inside that day's working hours
        that does not overlap any busy block.
        """)]
    [ExportAIFunction("get_calendar")]
    public static Task<string> GetCalendar(
        [Description("Date to read, like 2026-05-29. Start with today and walk forward one day at a time if you cannot find a free slot.")]
        DateOnly date,
        [FromServices] CalendarService calendar,
        CancellationToken cancellationToken = default)
        => calendar.GetCalendarAsync(date, cancellationToken);
}
