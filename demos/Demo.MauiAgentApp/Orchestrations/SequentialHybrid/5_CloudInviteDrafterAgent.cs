using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 5 of the meeting-invite pipeline. Runs in the cloud (Azure OpenAI).
///
/// Receives only the short prose brief from stage 3 — no addresses, no
/// phone numbers, no passwords, no card/SSN numbers, no JSON wrapper.
/// Uses the <see cref="LocalCalendarTool.GetCalendar"/> tool to find a slot
/// when the user is free, then writes the BODY of a meeting-invite email.
///
/// The body alone — subject, from, to lines are added by the local
/// <c>LocalInviteFinaliserExecutor</c> using fields read from workflow
/// state (the picked email's <c>SenderEmail</c> / <c>SenderName</c> /
/// <c>Subject</c>). The cloud never sees those.
///
/// The local-tool call is the demo's headline moment: the cloud agent
/// reaches BACK to the device for something only the device knows (the
/// user's calendar). The tool's input is the natural-language date, the
/// output is a free/busy view — no PII in either direction.
/// </summary>
public static partial class CloudInviteDrafterAgentExtensions
{
    [AIToolSource(typeof(LocalCalendarTool))]
    private partial class InviteToolContext : AIToolContext { }

    public static IHostApplicationBuilder AddCloudInviteDrafterAgent(
        this IHostApplicationBuilder builder, string name)
    {
        var tools = InviteToolContext.Default.Tools;

        builder.AddAIAgent(
            name,
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredKeyedService<IChatClient>(AIModels.Cloud),
                name: key,
                description:
                    """
                    Cloud-side meeting-invite drafter. Receives a short
                    customer-issue brief from the on-device summariser and
                    writes the BODY of a meeting-invite email, proposing a
                    free slot read on-device via the get_calendar tool.
                    """,
                instructions:
                    """
                    The user message you receive is a SHORT BRIEF written by
                    an on-device colleague summarising a customer email. It
                    contains the customer name and what they are asking for —
                    nothing else. Take it at face value; do not ask for more
                    information.

                    Your job:

                      1. Call the `get_calendar` tool ONCE with a natural-
                         language date like "tomorrow" or "this week". The
                         tool returns the user's WORKING HOURS and a list
                         of BUSY time ranges. There is no "free" tag — any
                         time inside working hours that is NOT inside a
                         busy range is fair game.
                      2. Propose a specific meeting time INSIDE working
                         hours and OUTSIDE every busy range. Pick a real
                         gap from the calendar — do not invent a time, do
                         not pick a time that overlaps a busy block, do
                         not pick a time outside working hours.
                      3. Write the BODY of a meeting-invite email
                         addressed to the customer named in the brief.

                    Use this exact Markdown structure for the body:

                        ## :event: Meeting invite — <one-line reason>

                        **Proposed time:** <date and time, e.g. "Tuesday 2pm">

                        Hi <first name>,

                        <2-3 sentences acknowledging their issue, referencing
                        any order/account ID from the brief, and proposing
                        the time>.

                        Looking forward to speaking soon.

                    Rules:
                      • Output ONLY the body shown above. Do NOT add a
                        "Subject:", "From:", "To:" line, or any YAML
                        frontmatter — those are added on-device after you
                        respond.
                      • Use specific facts from the brief (customer name,
                        order ID) so the invite feels personal.
                      • Pick a real gap between busy blocks from the
                        get_calendar response — do not invent one and do
                        not propose a time that overlaps any busy block.
                      • 4-6 sentences total. No sign-off, no extra notes.
                    """,
                tools: [.. tools]).WithTelemetry());

        return builder;
    }
}
