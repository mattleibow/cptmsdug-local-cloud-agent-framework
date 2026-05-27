using Demo.Orchestrations.SequentialHybrid.Executors;
using Demo.Orchestrations.SequentialHybrid.Models;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
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
//  │  inbox-picker            local agent                                  │
//  │    • TextSearchProvider → InboxService fabricates 3-5 fake emails    │
//  │      on-device, exposes them as RAG context.                          │
//  │    • Picks ONE email, returns structured PickedEmail JSON.            │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ PickedEmail JSON
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  body-redactor           local agent                                  │
//  │    • Reads the picked email body and returns a list of sensitive     │
//  │      entities it spotted, each tagged PERSON / COMPANY / PROJECT /   │
//  │      AMOUNT. It does NOT rewrite the body — substitution happens in  │
//  │      the next executor.                                               │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ RedactedBody JSON (list of entities)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  cloud-prompt-adapter    executor, no LLM                             │
//  │    • Parses PickedEmail + RedactedBody from the two assistant turns.  │
//  │    • Drops hallucinated entities that don't actually appear in the    │
//  │      body, assigns PERSON_n / COMPANY_n / … tokens, and runs literal  │
//  │      string.Replace over the body — deterministic redaction.          │
//  │    • Stores the picked email and the token → original mapping in     │
//  │      workflow state for the assembler to read at the end.             │
//  │    • Emits the cloud prompt — first names only, subject, redacted     │
//  │      body — and tells the cloud to draft just a reply BODY.           │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ ChatMessage (no last names, no emails, no $, no project names)
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  cloud-reply-writer      CLOUD agent                                  │
//  │    • Drafts a reply body. No greeting, no sign-off — the device       │
//  │      will assemble those. Re-uses tokens verbatim.                    │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ reply body text
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  final-email-assembler   executor, no LLM                             │
//  │    • Reads PickedEmail + redaction mapping from workflow state.       │
//  │    • Reads the cloud body from the latest assistant turn.             │
//  │    • Rehydrates any tokens the cloud preserved back to the original   │
//  │      values (literal string.Replace from the stored mapping).         │
//  │    • Emits a single Markdown document with YAML frontmatter and a     │
//  │      mailto: link — the DevUI's MarkdownLabel surfaces the link as    │
//  │      a clickable "Open in Mail" button via Launcher.OpenAsync.        │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ markdown
//          ▼
//  OutputMessagesExecutor → workflow.output
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
    public static void AddEmailTriageWorkflow(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<InboxService>();

        // 1. INBOX PICKER — local agent that does RAG over the fake inbox and
        //    picks the single email most relevant to the user's request.
        builder.AddAIAgent(
            "local-inbox-picker",
            (sp, key) =>
            {
                var inbox = sp.GetRequiredService<InboxService>();
                var ragProvider = new TextSearchProvider(
                    inbox.SearchAsync,
                    new TextSearchProviderOptions
                    {
                        SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                        RecentMessageMemoryLimit = 6,
                        ContextPrompt =
                            "## Inbox candidates\n" +
                            "Each entry below is a separate email from the user's inbox. " +
                            "Pick exactly ONE — the most relevant to the user's request.",
                        CitationsPrompt = string.Empty,
                    });

                return new ChatClientAgent(
                    sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                    new ChatClientAgentOptions
                    {
                        Name = key,
                        Description =
                            "On-device inbox picker: chooses the single inbox email most " +
                            "relevant to the user's request and returns it as structured JSON.",
                        AIContextProviders = [ragProvider],
                        ChatOptions = new ChatOptions
                        {
                            ResponseFormat = ChatResponseFormat.ForJsonSchema<PickedEmail>(),
                            Instructions = """
                                You are an on-device inbox picker. The context
                                lists the user's inbox emails, each with these
                                labels:

                                  SENDER_EMAIL, SENDER_NAME,
                                  SUBJECT, RECEIVED, body

                                Pick the ONE entry most relevant to the user's
                                request and copy its fields 1:1 into the schema:

                                  SENDER_EMAIL → senderEmail
                                  SENDER_NAME  → senderName
                                  SUBJECT      → subject
                                  body lines   → body
                                """,
                        },
                    });
            });

        // 2. BODY REDACTOR — local agent that SPOTS sensitive entities in
        //    the picked email body and returns them as a structured list.
        //    The actual replacement happens in the cloud-prompt-adapter, so
        //    this stage just decides what's sensitive — not how to format it.
        builder.AddAIAgent(
            "local-body-redactor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                new ChatClientAgentOptions
                {
                    Name = key,
                    Description =
                        "On-device entity spotter: scans the picked email body for last " +
                        "names, company names, project names, and dollar amounts and " +
                        "returns them as a structured list for the next executor to " +
                        "tokenise.",
                    ChatOptions = new ChatOptions
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<RedactedBody>(),
                        // The schema's [MaxLength(5)] per-list cap is the real
                        // bound on the redactor's output. Keep MaxOutputTokens
                        // generous enough for the 4 short lists.
                        MaxOutputTokens = 200,
                        Instructions = """
                            You are a privacy spotter. Read the PickedEmail body
                            and fill the four lists with substrings copied
                            character-for-character from the body:

                              personLastNames — last names only (no first names)
                              companies       — company / organisation names
                              projects        — project / product names
                              amounts         — dollar amounts like "$5,000"

                            If a category has none, return an empty list.
                            """,
                    },
                }));

        // 3. CLOUD REPLY WRITER — receives only first names, subject, and the
        //    token-redacted body. Drafts a plain reply body.
        builder.AddAIAgent(
            name: "cloud-reply-writer",
            instructions: """
                You are a senior email assistant. The user wants help drafting a reply
                to a colleague. You will be given:

                  - FROM:    the colleague's FIRST name
                  - TO:      the user's FIRST name
                  - SUBJECT: the subject line of the colleague's email
                  - The body of the colleague's email (with last names, company names,
                    project names, and dollar amounts already replaced by placeholder
                    tokens like PERSON_1, COMPANY_1, PROJECT_1, AMOUNT_1 — keep these
                    tokens VERBATIM in your output, do not invent names or numbers)

                Draft just the BODY of the reply. Do NOT include:
                  - any "Hi X" greeting
                  - any "Best, Y" sign-off
                  - any subject line

                The device will handle the greeting, sign-off, subject, and recipient.
                Keep your reply 3-6 sentences, professional, and grounded in what the
                colleague actually said. If there's a question to answer, answer it.
                If there's a request to acknowledge, acknowledge it. Re-use any tokens
                from the input where appropriate so the device can rehydrate them in
                the final user-facing email.
                """,
            description:
                "Cloud-side reply drafter — sees only first names + redacted body, " +
                "returns a reply body.",
            chatClientServiceKey: AIModels.Cloud);

        // ── Wire everything together ─────────────────────────────────────
        builder.AddWorkflow("local-cloud-email-triage", (sp, key) =>
        {
            var pickerAgent      = sp.GetRequiredKeyedService<AIAgent>("local-inbox-picker");
            var redactorAgent    = sp.GetRequiredKeyedService<AIAgent>("local-body-redactor");
            var cloudWriterAgent = sp.GetRequiredKeyedService<AIAgent>("cloud-reply-writer");
            var inbox            = sp.GetRequiredService<InboxService>();

            var hostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = true,
            };

            ExecutorBinding picker        = pickerAgent.BindAsExecutor(hostOpts);
            ExecutorBinding redactor      = redactorAgent.BindAsExecutor(hostOpts);
            ExecutorBinding cloudPrompt   = new CloudPromptAdapter(inbox);
            ExecutorBinding cloudWriter   = cloudWriterAgent.BindAsExecutor(hostOpts);
            ExecutorBinding finalAssembly = new FinalEmailAssembler(inbox);
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
