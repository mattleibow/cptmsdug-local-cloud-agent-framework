using System.ComponentModel;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;

/// <summary>
/// Stands in for "the user's local calendar" in the meeting-invite demo.
///
/// The cloud invite-drafter calls into this via the <c>get_calendar</c>
/// tool to plan a meeting time. The cloud never sees the calendar itself —
/// only an opaque view: the user's working hours plus a list of busy
/// time ranges. No event titles, no attendees, no projects. The drafter
/// has to propose a meeting time INSIDE working hours and OUTSIDE any
/// busy block — it cannot just grep for a "free" tag.
///
/// In a real app this would read EventKit / Microsoft Graph. For the
/// demo we fabricate the schedule per call so each run feels fresh.
/// We use the CLOUD chat client because the data is invented and the
/// cloud model produces richer, more varied schedules — the privacy
/// story is preserved by the SHAPE of the data returned (working hours
/// + opaque busy ranges) rather than by where it's generated.
/// </summary>
public sealed class CalendarService([FromKeyedServices(AIModels.Cloud)] IChatClient cloudChatClient)
{
    /// <summary>
    /// Returns a Markdown view of the user's calendar for the given date
    /// or range: working hours on one line, then an opaque list of busy
    /// time ranges. Never names an event, never names a person.
    /// </summary>
    public async Task<string> GetCalendarAsync(
        string dateOrRange,
        CancellationToken cancellationToken)
    {
        var response = await cloudChatClient.GetResponseAsync<GeneratedSchedule>(
        [
            new(ChatRole.System,
                """
                You are a calendar generator for a privacy demo. Given a
                date or range like "tomorrow", "this week", "Tuesday",
                invent a plausible busy schedule for the user.

                Output shape:
                  • workingHoursStart / workingHoursEnd: when the user is
                    available in principle. Pick a plausible workday like
                    08:30–17:30 or 09:00–18:00 — vary slightly across
                    requests.
                  • busyBlocks: 4-7 ordered ranges INSIDE working hours,
                    representing meetings or focus blocks. Use a mix of
                    short (15-30 min) and longer (45-90 min) blocks with
                    real gaps of 15-90 minutes between them. The busy
                    blocks MUST NOT overlap each other.

                CRITICAL — privacy:
                  • Do NOT include any event name, title, attendee, room,
                    project, or other label. The schema has no such field.
                    The busy blocks are OPAQUE — the only information that
                    leaves the device is "the user is busy from A to B".

                Return JSON matching the schema.
                """),
            new(ChatRole.User, $"Date or range: {dateOrRange}")
        ],
        new ChatOptions
        {
            MaxOutputTokens = 600,
            // High temperature so subsequent runs aren't identical
            // schedules.
            Temperature = 1.0f,
            TopP = 0.95f,
        },
        cancellationToken: cancellationToken);

        if (!response.TryGetResult(out var schedule) || schedule.BusyBlocks is null)
            return "_(could not read calendar)_";

        var md = new StringBuilder()
            .Append("Working hours: ")
            .Append(schedule.WorkingHoursStart)
            .Append('–')
            .Append(schedule.WorkingHoursEnd)
            .AppendLine()
            .AppendLine()
            .AppendLine("Busy:");
        foreach (var b in schedule.BusyBlocks)
            md.Append("  - ").Append(b.Start).Append('–').AppendLine(b.End);
        return md.ToString();
    }

    [Description("Opaque view of the user's calendar — working hours and busy time ranges, no event titles.")]
    private sealed record GeneratedSchedule(
        [property: Description("Start of the user's available window for the day, like \"09:00\".")]
        string WorkingHoursStart,

        [property: Description("End of the user's available window for the day, like \"17:30\".")]
        string WorkingHoursEnd,

        [property: Description("Time-ordered busy ranges inside working hours. Mix short and longer blocks with realistic gaps between them. Must not overlap each other or fall outside working hours.")]
        List<BusyBlock> BusyBlocks);

    private sealed record BusyBlock(
        [property: Description("Start time of a busy block, like \"10:30\".")]
        string Start,

        [property: Description("End time of a busy block, like \"11:15\".")]
        string End);
}
