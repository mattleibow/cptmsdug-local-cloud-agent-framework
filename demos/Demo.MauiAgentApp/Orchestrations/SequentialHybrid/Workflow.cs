using Demo.MauiAgentApp.Orchestrations.SequentialHybrid;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.MauiAgentApp.Orchestrations;

// ════════════════════════════════════════════════════════════════════════════
//  local-cloud-meeting-invite workflow
// ════════════════════════════════════════════════════════════════════════════
//
//  USER INPUT (e.g. "draft a meeting invite to resolve the latest issue")
//          │
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  1. local-inbox-search          LOCAL agent + LOCAL RAG               │
//  │    • TextSearchProvider → InboxService fabricates 3-5 customer        │
//  │      emails with realistic PII (addresses, phone numbers, passwords,  │
//  │      cards, SSNs alongside the actual issue).                         │
//  │    • Picks ONE entry, returns structured PickedEmail JSON.            │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ PickedEmail JSON
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  2. local-issue-summariser      LOCAL agent — plain prose             │
//  │    • Writes a SHORT brief (3-4 sentences) the cloud can use to draft  │
//  │      a meeting invite.                                                │
//  │    • Keeps the customer's name and any order / account / case ID —    │
//  │      the support flow can't function without those.                   │
//  │    • Explicitly drops physical addresses, phone numbers, passwords,   │
//  │      credit-card and SSN numbers.                                     │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ plain-text brief
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  3. cloud-invite-drafter        CLOUD agent + LOCAL TOOL              │
//  │    • Calls `get_calendar(dateOrRange)` once. The tool runs LOCALLY    │
//  │      against CalendarService — the cloud agent reaches back to the    │
//  │      device for something only the device knows (the user's calendar).│
//  │    • Drafts a Markdown invite proposing a slot the user is free.      │
//  │    • Sees only the brief + the free/busy view — never the customer's  │
//  │      original email, address, phone, password, card, etc.             │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ markdown invite draft
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  5. local-invite-finaliser      executor, no LLM                      │
//  │    • Wraps the draft in YAML frontmatter (rendered as a monospaced    │
//  │      metadata header by the DevUI MarkdownLabel).                     │
//  │    • Adds a clickable mailto: link the user can tap to open the       │
//  │      invite in their default email client.                            │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ markdown
//          ▼
//  6. OutputMessagesExecutor → workflow.output
//
// ─── Privacy contract ─────────────────────────────────────────────────────
//   What the cloud sees:
//     • A short prose brief (3-4 sentences)
//     • Free/busy view of the user's calendar (via tool call — no event
//       titles, no attendees, no project names)
//
//   What the cloud NEVER sees:
//     • The customer's physical address
//     • Phone numbers
//     • Passwords / credentials
//     • Credit-card or bank-account numbers
//     • SSNs
//     • The user's actual calendar events (only free/busy)
//     • The full inbox
//     • The customer's original email
// ──────────────────────────────────────────────────────────────────────────

public static class MeetingInviteWorkflow
{
    public const string Name = "local-cloud-meeting-invite";

    public static void AddMeetingInviteWorkflow(this IHostApplicationBuilder builder)
    {
        const string searchAgent     = "local-inbox-search";
        const string summariserAgent = "local-issue-summariser";
        const string drafterAgent    = "cloud-invite-drafter";

        builder.Services.AddSingleton<InboxService>();
        builder.Services.AddSingleton<CalendarService>();

        builder
            .AddLocalInboxSearchAgent(searchAgent)
            .AddLocalIssueSummariserAgent(summariserAgent)
            .AddCloudInviteDrafterAgent(drafterAgent);

        builder.AddWorkflow(Name, (sp, key) =>
        {
            var search     = sp.GetRequiredKeyedService<AIAgent>(searchAgent);
            var summariser = sp.GetRequiredKeyedService<AIAgent>(summariserAgent);
            var drafter    = sp.GetRequiredKeyedService<AIAgent>(drafterAgent);
            var inbox      = sp.GetRequiredService<InboxService>();

            var hostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = true,
            };

            ExecutorBinding e1 = search.BindAsExecutor(hostOpts);
            ExecutorBinding e2 = summariser.BindAsExecutor(hostOpts);
            ExecutorBinding e3 = drafter.BindAsExecutor(hostOpts);
            ExecutorBinding e5 = new LocalInviteFinaliserExecutor(inbox);
            ExecutorBinding e6 = new OutputMessagesExecutor();

            return new WorkflowBuilder(e1)
                .AddEdge(e1, e2)
                .AddEdge(e2, e3)
                .AddEdge(e3, e5)
                .AddEdge(e5, e6)
                .WithOutputFrom(e6)
                .WithName(key)
                .WithDescription(
                    """
                    Local inbox-search → local issue summariser → cloud
                    invite drafter (with on-device get_calendar tool) →
                    on-device finaliser. The cloud sees only the redacted
                    brief and the calendar's free/busy slots — never the
                    customer's address, phone, password, card, SSN, or the
                    actual calendar events.
                    """)
                .Build();
        }).AddAsAIAgent();
    }
}
