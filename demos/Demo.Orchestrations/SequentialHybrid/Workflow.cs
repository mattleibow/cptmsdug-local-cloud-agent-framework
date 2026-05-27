using Demo.Orchestrations.SequentialHybrid;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations;

// ════════════════════════════════════════════════════════════════════════════
//  local-cloud-email-triage workflow
// ════════════════════════════════════════════════════════════════════════════
//
//  USER INPUT (free-text request, e.g. "reply to Bob about budget")
//          │
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  1_LocalInboxPickerAgent            local agent                       │
//  │    • TextSearchProvider → InboxService fabricates 3-5 fake emails    │
//  │      on-device, exposes them as RAG context.                          │
//  │    • Picks ONE email, returns structured PickedEmail JSON.            │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ PickedEmail JSON
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  2_LocalBodyRedactorAgent           local agent                       │
//  │    • Reads the picked email body and returns FOUR typed lists of     │
//  │      substrings it spotted — last names, companies, projects, dollar │
//  │      amounts. The lists are bounded by [MaxLength(5)] so the         │
//  │      constrained decoder cannot run away.                            │
//  │    • Does NOT rewrite the body — substitution happens in the next    │
//  │      executor where it can be done deterministically.                │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ RedactedBody JSON (four typed lists)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  3_CloudPromptAdapterExecutor       executor, no LLM                  │
//  │    • Parses PickedEmail + RedactedBody from the two assistant turns. │
//  │    • Drops hallucinated substrings that don't actually appear in the │
//  │      body, assigns PERSON_n / COMPANY_n / PROJECT_n / AMOUNT_n       │
//  │      tokens and runs literal string.Replace over the body —          │
//  │      deterministic redaction, no LLM involvement.                    │
//  │    • Stores the picked email and the token → original mapping in     │
//  │      workflow state for the assembler to read at the end.            │
//  │    • Emits the cloud prompt — sender + recipient first names only,   │
//  │      subject, and the redacted body. Tells the cloud to draft just   │
//  │      the reply body.                                                 │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage (no last names, no emails, no $, no project names)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  4_CloudReplyWriterAgent            CLOUD agent                       │
//  │    • Drafts a reply body. No greeting, no sign-off — the device      │
//  │      will assemble those. Re-uses tokens verbatim.                   │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ reply body text
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  5_FinalEmailAssemblerExecutor      executor, no LLM                  │
//  │    • Reads PickedEmail + redaction mapping from workflow state.      │
//  │    • Reads the cloud body from the latest assistant turn.            │
//  │    • Rehydrates any tokens the cloud preserved back to the original  │
//  │      values (literal string.Replace from the stored mapping).        │
//  │    • Emits a single Markdown document with YAML frontmatter and a    │
//  │      mailto: link — the DevUI's MarkdownLabel surfaces the link as   │
//  │      a clickable "Open in Mail" button via Launcher.OpenAsync.       │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ markdown
//          ▼
//  6_OutputMessagesExecutor → workflow.output
//
// ─── Privacy contract ─────────────────────────────────────────────────────
//   Leaves the device → cloud:
//     • First names (sender + user)
//     • Subject line
//     • Body with last names, companies, projects, and dollar amounts
//       already swapped for tokens
//
//   Never leaves the device:
//     • Last names
//     • Email addresses
//     • Company / org names appearing inside the body
//     • Project / product names
//     • Dollar amounts
//     • The full inbox
// ──────────────────────────────────────────────────────────────────────────

public static class EmailTriageWorkflow
{
    public const string Name = "local-cloud-email-triage";

    public static void AddEmailTriageWorkflow(this IHostApplicationBuilder builder)
    {
        const string pickerName   = "local-inbox-picker";
        const string redactorName = "local-body-redactor";
        const string writerName   = "cloud-reply-writer";

        builder.Services.AddSingleton<InboxService>();

        builder
            .AddLocalInboxPickerAgent(pickerName)
            .AddLocalBodyRedactorAgent(redactorName)
            .AddCloudReplyWriterAgent(writerName);

        builder.AddWorkflow(Name, (sp, key) =>
        {
            var pickerAgent      = sp.GetRequiredKeyedService<AIAgent>(pickerName);
            var redactorAgent    = sp.GetRequiredKeyedService<AIAgent>(redactorName);
            var cloudWriterAgent = sp.GetRequiredKeyedService<AIAgent>(writerName);
            var inbox            = sp.GetRequiredService<InboxService>();

            var hostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = true,
            };

            ExecutorBinding picker        = pickerAgent.BindAsExecutor(hostOpts);
            ExecutorBinding redactor      = redactorAgent.BindAsExecutor(hostOpts);
            ExecutorBinding cloudPrompt   = new CloudPromptAdapterExecutor(inbox);
            ExecutorBinding cloudWriter   = cloudWriterAgent.BindAsExecutor(hostOpts);
            ExecutorBinding finalAssembly = new FinalEmailAssemblerExecutor(inbox);
            ExecutorBinding output        = new OutputMessagesExecutor();

            return new WorkflowBuilder(picker)
                .AddEdge(picker, redactor)
                .AddEdge(redactor, cloudPrompt)
                .AddEdge(cloudPrompt, cloudWriter)
                .AddEdge(cloudWriter, finalAssembly)
                .AddEdge(finalAssembly, output)
                .WithOutputFrom(output)
                .WithName(key)
                .WithDescription(
                    "Local inbox-picker → local body-redactor → cloud reply writer → " +
                    "on-device assembly. The cloud sees only first names, the subject, " +
                    "and a token-redacted body — never last names, email addresses, " +
                    "company names, project names, or dollar amounts from the body.")
                .Build();
        }).AddAsAIAgent();
    }
}
