using System.Globalization;
using System.Text;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;

/// <summary>
/// Stands in for "the user's local calendar" in the meeting-invite demo.
///
/// The cloud invite-drafter calls into this via the <c>get_calendar</c>
/// tool to plan a meeting time. The cloud never sees the calendar itself
/// — only an opaque view: a fixed 09:00–17:00 working window plus a list
/// of busy time ranges. No event titles, no attendees, no projects.
///
/// In a real app this would read EventKit / Microsoft Graph. For the
/// demo we generate the schedule deterministically per date — calling
/// the tool twice for the same date returns the same calendar (matches
/// a real calendar) but consecutive days differ:
///
///   • Working hours are fixed at <see cref="WorkingHoursStartMinutes"/>
///     to <see cref="WorkingHoursEndMinutes"/>. Never randomised.
///   • ~30% of dates land on a "fully booked" day — the whole working
///     window is back-to-back blocks with no usable gap. The agent
///     should detect this and call the tool again for a later date.
///   • The other ~70% of dates have 0-4 busy blocks of 30-120 minutes
///     placed inside the window with random gaps.
/// </summary>
public sealed class CalendarService
{
    private const int WorkingHoursStartMinutes = 9 * 60;   // 09:00
    private const int WorkingHoursEndMinutes   = 17 * 60;  // 17:00
    private const int FullyBookedPercent       = 30;

    /// <summary>
    /// Returns a Markdown view of the user's calendar for the given date:
    /// the date header, fixed working hours, and an opaque list of busy
    /// ranges. Never names an event, never names a person.
    /// </summary>
    public Task<string> GetCalendarAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        // Deterministic per-date seed: same date → same calendar; the
        // tool behaves like a real calendar where re-reading doesn't
        // shuffle your schedule.
        var rng = new Random(date.DayNumber);

        var fullyBooked = rng.Next(0, 100) < FullyBookedPercent;
        var blocks = fullyBooked ? BuildFullyBookedDay(rng) : BuildNormalDay(rng);

        var md = new StringBuilder()
            .Append("Calendar for ")
            .Append(date.ToString("yyyy-MM-dd (dddd)", CultureInfo.InvariantCulture))
            .AppendLine()
            .AppendLine()
            .Append("Working hours: ")
            .Append(FormatTime(WorkingHoursStartMinutes))
            .Append('–')
            .Append(FormatTime(WorkingHoursEndMinutes))
            .AppendLine();

        if (blocks.Count == 0)
        {
            md.AppendLine().AppendLine("Busy: _(none)_");
        }
        else
        {
            md.AppendLine().AppendLine("Busy:");
            foreach (var (s, e) in blocks)
                md.Append("  - ").Append(FormatTime(s)).Append('–').AppendLine(FormatTime(e));
        }

        return Task.FromResult(md.ToString());
    }

    // Walk forward through the working day, picking gaps and blocks of
    // random length — bail out cleanly when there's no room left.
    private static List<(int Start, int End)> BuildNormalDay(Random rng)
    {
        var blocks = new List<(int Start, int End)>();
        int blockCount = rng.Next(0, 5);              // 0..4 inclusive
        int cursor = WorkingHoursStartMinutes + 15;    // brief lead-in

        for (int i = 0; i < blockCount; i++)
        {
            // Random gap before this block (0..90 min, snapped to 15 min).
            cursor += rng.Next(0, 7) * 15;
            int duration = rng.Next(1, 5) * 30;        // 30..120 min
            if (cursor + duration > WorkingHoursEndMinutes - 15)
                break;

            blocks.Add((cursor, cursor + duration));
            cursor += duration;
        }

        return blocks;
    }

    // Back-to-back blocks covering the full window. Built as 30-90 min
    // chunks so the demo's "fully booked" view still looks like real
    // meetings rather than one giant slab.
    private static List<(int Start, int End)> BuildFullyBookedDay(Random rng)
    {
        var blocks = new List<(int Start, int End)>();
        int cursor = WorkingHoursStartMinutes;

        while (cursor < WorkingHoursEndMinutes)
        {
            int duration = rng.Next(1, 4) * 30;        // 30..90 min
            int end = Math.Min(cursor + duration, WorkingHoursEndMinutes);
            blocks.Add((cursor, end));
            cursor = end;
        }

        return blocks;
    }

    private static string FormatTime(int totalMinutes) =>
        $"{totalMinutes / 60:D2}:{totalMinutes % 60:D2}";
}
