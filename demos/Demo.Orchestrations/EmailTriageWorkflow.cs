using Demo.Orchestrations.Services;
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
//  │    • Replaces last names, companies, projects, and dollar amounts    │
//  │      mentioned IN the body with PERSON_n / COMPANY_n / PROJECT_n /   │
//  │      AMOUNT_n tokens. First names stay.                               │
//  │    • Returns RedactedBody { body, mapping }.                          │
//  └──────────────────────────────────────────────────────────────────────┘
//          │ RedactedBody JSON
//          ▼
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  cloud-prompt-adapter    executor, no LLM                             │
//  │    • Parses PickedEmail + RedactedBody from the two assistant turns.  │
//  │    • Stores both in workflow state for the assembler.                 │
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
//  │    • Reads PickedEmail from workflow state (for to/from/subject).     │
//  │    • Reads cloud body from the latest assistant turn.                 │
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
                            Instructions = $$"""
                                You are an on-device inbox picker. You will see a list of
                                inbox emails in context. Pick the ONE email most relevant
                                to the user's request and return it as a JSON object
                                matching this exact shape:

                                  {
                                    "fromFullName": "Bob Martinez",
                                    "fromEmail":    "bob.martinez@acme-corp.com",
                                    "toFullName":   "{{UserProfile.Name}}",
                                    "toEmail":      "{{UserProfile.Email}}",
                                    "subject":      "Q3 budget approval",
                                    "body":         "<full original email body, verbatim>"
                                  }

                                Rules:
                                  - Copy the FROM_NAME / FROM_EMAIL / TO_NAME / TO_EMAIL
                                    fields exactly as shown in the chosen inbox entry.
                                  - Copy the Subject exactly.
                                  - Copy the body verbatim — preserve all paragraphs,
                                    quotes, dates, dollar amounts, and named people.
                                  - Do not invent emails. If no inbox entry is a good
                                    match, pick the closest one anyway.
                                """,
                        },
                    });
            });

        // 2. BODY REDACTOR — local agent that swaps last names and company
        //    names INSIDE the body for placeholder tokens. The token map is
        //    purely informational; we do not rehydrate it into the final
        //    reply body (per design: the user-facing email uses originals,
        //    only the cloud-bound prompt uses tokens).
        builder.AddAIAgent(
            "local-body-redactor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                new ChatClientAgentOptions
                {
                    Name = key,
                    Description =
                        "On-device body redactor: replaces last names and company names " +
                        "inside the email body with placeholder tokens before the cloud " +
                        "sees the text.",
                    ChatOptions = new ChatOptions
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<RedactedBody>(),
                        Instructions = """
                            You are an on-device privacy redactor. You will be given a
                            PickedEmail JSON from the previous stage. Your job is to take
                            the `body` field, find every sensitive token inside it, and
                            replace each with a stable placeholder.

                            Token scheme — re-use the same token for the same value:
                              - Last names      → PERSON_1, PERSON_2, ...
                              - Companies/orgs  → COMPANY_1, COMPANY_2, ...
                              - Projects/products → PROJECT_1, PROJECT_2, ...
                              - Dollar amounts  → AMOUNT_1, AMOUNT_2, ...

                            FIRST NAMES STAY. So "Bob Lobby from Choppy Corp pitched
                            Project Zen for $42,000" becomes "Bob PERSON_1 from
                            COMPANY_1 pitched PROJECT_1 for AMOUNT_1".

                            Do not redact anything outside the body. Do not redact the
                            subject. Do not redact email addresses (they are handled
                            separately and never leave the device).

                            Return JSON matching this exact shape:

                              {
                                "body": "<the body verbatim, with last names,
                                          companies, projects, and dollar amounts
                                          swapped for tokens>",
                                "mapping": [
                                  { "token": "PERSON_1",  "original": "Lobby" },
                                  { "token": "COMPANY_1", "original": "Choppy Corp" },
                                  { "token": "PROJECT_1", "original": "Project Zen" },
                                  { "token": "AMOUNT_1",  "original": "$42,000" }
                                ]
                              }

                            If there is nothing to redact, return the body unchanged and
                            an empty mapping array.
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

                  - FROM: the colleague's FIRST name
                  - TO:   the user's FIRST name
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

            var hostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = true,
            };

            ExecutorBinding picker        = pickerAgent.BindAsExecutor(hostOpts);
            ExecutorBinding redactor      = redactorAgent.BindAsExecutor(hostOpts);
            ExecutorBinding cloudPrompt   = new CloudPromptAdapter();
            ExecutorBinding cloudWriter   = cloudWriterAgent.BindAsExecutor(hostOpts);
            ExecutorBinding finalAssembly = new FinalEmailAssembler();
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
