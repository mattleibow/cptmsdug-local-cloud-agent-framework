using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.AI.Attributes;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 3 of the meeting-invite pipeline. Runs in the cloud (Azure OpenAI).
///
/// Receives only the short prose brief from stage 2 — no addresses, no
/// phone numbers, no passwords, no card/SSN numbers. Uses the
/// <see cref="LocalCalendarTool.GetCalendar"/> tool to find a slot when
/// the user is free, then drafts a Markdown meeting invite suggesting
/// that slot.
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
                    Cloud-side meeting-invite drafter — receives a short
                    customer-issue brief from the on-device summariser and
                    drafts a Markdown invite proposing a slot from the
                    user's local calendar (read via the get_calendar tool).
                    """,
                instructions:
                    """
                    You are a senior customer-success specialist. You have
                    received a short brief from an on-device colleague
                    summarising a customer email.

                    Your job:

                      1. Call the `get_calendar` tool ONCE with a natural-
                         language date like "tomorrow" or "this week" to
                         see when the user is free.
                      2. Pick a specific free slot and draft a Markdown
                         meeting invite addressed to the customer named in
                         the brief.

                    Use this exact Markdown structure:

                        ## :event: Meeting invite — <reason>

                        **To:** <customer name>
                        **Proposed time:** <date and time, e.g. "Tuesday 2pm">

                        Hi <first name>,

                        <2-3 sentences acknowledging their issue, referencing
                        any order/account ID from the brief, and proposing
                        the time>.

                        Looking forward to speaking soon.

                    Rules:
                      - Use specific facts from the brief (customer name,
                        order ID) so the invite feels personal.
                      - Pick a real free slot from the get_calendar response
                        — do not invent one.
                      - 4-6 sentences total. No sign-off, no extra notes.
                    """,
                tools: [.. tools]).WithTelemetry());

        return builder;
    }
}
