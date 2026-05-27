using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;

/// <summary>
/// Stands in for "the user's local calendar" in the meeting-invite demo.
///
/// The cloud invite-drafter calls into this via the <c>get_calendar</c>
/// tool to find a slot when the user is free. The cloud never sees the
/// raw calendar — only the free/busy summary the tool returns.
///
/// In a real app this would read EventKit / Microsoft Graph. For the demo
/// we fabricate a plausible schedule per call so each demo run feels
/// fresh. We use the LOCAL chat client because a real calendar would
/// only ever be read locally — using the cloud for fabrication would
/// break the privacy story even if it's the easier path.
/// </summary>
public sealed class CalendarService([FromKeyedServices(AIModels.Local)] IChatClient localChatClient)
{
    /// <summary>
    /// Returns a Markdown free/busy view of the user's calendar for the
    /// given day or range. Generic event labels only — no attendees, no
    /// titles like "1:1 with Sarah".
    /// </summary>
    public async Task<string> GetCalendarAsync(
        string dateOrRange,
        CancellationToken cancellationToken)
    {
        var response = await localChatClient.GetResponseAsync<GeneratedSchedule>(
        [
            new(ChatRole.System,
                """
                You are a fake personal-calendar generator. Given a date or
                range like "tomorrow", "this week", "Tuesday", invent a
                plausible-but-realistic work schedule for the user. Aim for
                a mix of meetings and free slots — 4-6 entries per day.

                Use only generic labels — "Standup", "Internal review",
                "Lunch", "Focus block", "Customer call", "1:1". Never name
                colleagues or projects, because the calendar's contents
                themselves are private.

                Return JSON matching the schema.
                """),
            new(ChatRole.User, $"Date or range: {dateOrRange}")
        ],
        new ChatOptions { MaxOutputTokens = 600 },
        cancellationToken: cancellationToken);

        if (!response.TryGetResult(out var schedule) || schedule.Slots is null)
            return "_(could not read calendar)_";

        var lines = schedule.Slots.Select(s =>
            $"- **{s.Time}**  {s.Label}{(s.IsFree ? " _(free)_" : "")}");
        return string.Join("\n", lines);
    }

    [Description("A plausible schedule for one day or short range.")]
    private sealed record GeneratedSchedule(
        [property: Description("Time-ordered slots. Mix free blocks and meetings.")]
        List<ScheduleSlot> Slots);

    private sealed record ScheduleSlot(
        [property: Description("Time range like \"09:30-10:00\" or \"14:00-15:30\".")]
        string Time,

        [property: Description("Generic label like \"Standup\", \"Focus block\", \"Customer call\", or \"Free\".")]
        string Label,

        [property: Description("True if this slot is open for a new meeting, false if already booked.")]
        bool IsFree);
}
