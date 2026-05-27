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
//  │  1. local-inbox-search           LOCAL agent + LOCAL RAG              │
//  │     TextSearchProvider → InboxService fabricates 3-5 customer         │
//  │     emails with realistic PII (addresses, phones, passwords, cards,   │
//  │     SSNs) alongside the actual issue. Picks ONE entry, returns it     │
//  │     as a PickedEmail JSON.                                            │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(assistant, PickedEmail JSON)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  2. local-picker-to-state        LOCAL executor — no LLM              │
//  │     Parses the JSON, stores the typed PickedEmail in workflow state   │
//  │     under "picked-email" so the final stage can read back the         │
//  │     subject + recipient address without the cloud ever seeing them.   │
//  │     Forwards a SINGLE user-role message containing only the email     │
//  │     body downstream — drops the JSON wrapper and the original user    │
//  │     prompt.                                                           │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(user, email body)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  3. local-issue-summariser       LOCAL agent — plain prose            │
//  │     Sees ONLY the body. Writes a 2-3 sentence brief — keeps customer  │
//  │     name + order/account IDs, strips addresses, phones, passwords,    │
//  │     credit-card and SSN numbers.                                      │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(assistant, summary)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  4. local-summary-to-cloud       LOCAL executor — no LLM              │
//  │     One-way valve. Takes the summariser's last assistant message and  │
//  │     forwards a single fresh user-role message containing only that    │
//  │     summary. Nothing else from the prior turns crosses the boundary.  │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(user, summary)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  5. cloud-invite-drafter         CLOUD agent + LOCAL TOOL             │
//  │     Sees only the summary. Calls get_calendar(dateOrRange) once. The  │
//  │     tool runs LOCALLY against CalendarService — the cloud reaches     │
//  │     back to the device for something only the device knows (the      │
//  │     user's calendar). Writes the BODY of a meeting-invite email —    │
//  │     no subject/from/to lines (the finaliser adds those on-device).   │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(assistant, invite body)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  6. local-invite-finaliser       LOCAL executor — no LLM              │
//  │     Reads the PickedEmail back from workflow state ("picked-email")   │
//  │     and assembles the full envelope: YAML frontmatter with real       │
//  │     subject/from/to + the cloud's body + a mailto: link the user      │
//  │     can tap to open the invite in Mail with everything pre-filled.    │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage(assistant, full markdown)
//          ▼
//  7. local-output-messages → workflow.output
//
// ─── Privacy contract ─────────────────────────────────────────────────────
//   What the cloud sees:
//     • The summariser's 2-3 sentence brief (customer name + IDs only)
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
//     • The customer's email address or full name (the finaliser reads
//       these from workflow state to build the envelope ON-DEVICE)
//     • The full inbox
//     • The customer's original email body
//     • The original user prompt
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

            // Each LLM agent in this workflow sits behind a small executor
            // that emits exactly the message its successor needs. Turning
            // off ForwardIncomingMessages everywhere means no upstream
            // history (JSON, original user prompt, system framing) ever
            // leaks downstream — the only thing each agent sees is what
            // the executor immediately above it explicitly emitted.
            var scopedHostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = false,
            };

            ExecutorBinding inboxSearch     = search.BindAsExecutor(scopedHostOpts);
            ExecutorBinding pickerToState   = new LocalPickerToStateExecutor(inbox);
            ExecutorBinding issueSummariser = summariser.BindAsExecutor(scopedHostOpts);
            ExecutorBinding summaryToCloud  = new LocalSummaryToCloudExecutor();
            ExecutorBinding inviteDrafter   = drafter.BindAsExecutor(scopedHostOpts);
            ExecutorBinding inviteFinaliser = new LocalInviteFinaliserExecutor(inbox);
            ExecutorBinding outputMessages  = new LocalOutputMessagesExecutor();

            return new WorkflowBuilder(inboxSearch)
                .AddEdge(inboxSearch,     pickerToState)
                .AddEdge(pickerToState,   issueSummariser)
                .AddEdge(issueSummariser, summaryToCloud)
                .AddEdge(summaryToCloud,  inviteDrafter)
                .AddEdge(inviteDrafter,   inviteFinaliser)
                .AddEdge(inviteFinaliser, outputMessages)
                .WithOutputFrom(outputMessages)
                .WithName(key)
                .WithDescription(
                    """
                    Local inbox-search → picker→state → local summariser →
                    summary→cloud → cloud invite-drafter (with on-device
                    get_calendar tool) → on-device finaliser. Each stage
                    sees only what the executor above it explicitly
                    forwards. The cloud sees only the summary brief and
                    the calendar's free/busy slots — never the customer's
                    address, phone, password, card, SSN, email address,
                    or the actual calendar events.
                    """)
                .Build();
        }).AddAsAIAgent();
    }
}
