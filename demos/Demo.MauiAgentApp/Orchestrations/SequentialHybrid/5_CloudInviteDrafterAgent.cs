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

                      1. Call the `get_calendar` tool with TODAY's date
                         (use a real YYYY-MM-DD value). The tool returns
                         ONE day at a time: a fixed working window
                         (09:00–17:00) and an OPAQUE list of BUSY time
                         ranges. Any time inside working hours that is
                         not inside a busy range is free.
                      2. If today is fully booked (the busy blocks
                         cover the whole window with no usable gap),
                         call the tool again for the NEXT day. Walk
                         forward one day at a time, up to about three
                         attempts, until you find a day with a real
                         gap. Do not invent a time.
                      3. Pick a specific real gap (inside working hours,
                         outside every busy block) on the chosen day and
                         write the BODY of a meeting-invite email
                         addressed to the customer named in the brief.

                    Use this exact Markdown structure for the body:

                        ## :event: Meeting invite — <one-line reason>

                        **Proposed time:** <date and time, e.g. "Tue 2 Jun, 14:00">

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
                      • The proposed time must be a real gap from a
                        get_calendar response — never overlap a busy
                        block, never fall outside 09:00–17:00.
                      • 4-6 sentences total. No sign-off, no extra notes.
                    """,
                tools: [.. tools]).WithTelemetry());

        return builder;
    }
}
