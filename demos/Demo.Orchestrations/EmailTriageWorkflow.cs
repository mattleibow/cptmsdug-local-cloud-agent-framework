using Demo.Orchestrations.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations;

// ──────────────────────────────────────────────────────────────────────────────
// Local + cloud email triage workflow.
//
//   local-email-redactor   → on-device. A TextSearchProvider invents 3-5 fake
//                            inbox emails relevant to the user's request and
//                            injects them as RAG context. The redactor then
//                            tokenises every name / email / org / project and
//                            stores the mapping in workflow state via a
//                            hook-supplied tool.
//
//   cloud-reply-writer     → cloud. Sees only the tokenised text. Drafts the
//                            reply against placeholders like PERSON_1, ORG_2.
//
//   local-email-finisher   → on-device. Calls a hook-supplied tool that reads
//                            the redaction map back from workflow state and
//                            rehydrates the cloud's draft.
//
// The cloud agent never sees a real name, address, or company.
// ──────────────────────────────────────────────────────────────────────────────

public static class EmailTriageWorkflow
{
    public const string RedactionMapStateKey = "redaction_map";

    public static void AddEmailTriageWorkflow(this IHostApplicationBuilder builder)
    {
        // The inbox is fabricated on-device on demand — see InboxService.
        builder.Services.AddSingleton<InboxService>();

        // 1. Redactor — local. RAG via TextSearchProvider auto-injects fake
        //    inbox emails generated on-device, then the agent tokenises PII.
        builder.AddAIAgent(
            "local-email-redactor",
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
                            "## Inbox\n" +
                            "The following emails were retrieved from the user's inbox " +
                            "for context. They contain sensitive personal data — your " +
                            "job is to redact it before forwarding downstream.",
                        CitationsPrompt = string.Empty,
                    });

                return new ChatClientAgent(
                    sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                    new ChatClientAgentOptions
                    {
                        Name = key,
                        Description =
                            "On-device redactor: pulls the inbox via RAG, " +
                            "swaps every name / email / org / project for a " +
                            "stable placeholder token, and emits a structured " +
                            "JSON document with the mapping for the next stage.",
                        AIContextProviders = [ragProvider],
                        ChatOptions = new ChatOptions
                        {
                            ResponseFormat = ChatResponseFormat.ForJsonSchema<RedactionResult>(),
                            Instructions = $$"""
                                You are an on-device privacy redactor. You will be given:
                                  1. The user's request (what they want a reply about).
                                  2. A list of inbox emails injected as context by the
                                     RAG provider.

                                The user (the inbox owner) is:
                                  Name:  {{UserProfile.Name}}
                                  Email: {{UserProfile.Email}}

                                Your job is to produce a TOKEN-REDACTED, VERBATIM copy
                                of the relevant inbox email(s), preserving the full
                                content. You are NOT summarising, NOT shortening, NOT
                                paraphrasing — you are just running a find-and-replace
                                over the original text.

                                Build a token mapping. For EVERY occurrence of sensitive
                                data in the inbox emails AND the user's request, assign
                                a stable placeholder token. Use the SAME token for the
                                SAME value across the whole conversation:

                                  - The user themselves        → PERSON_USER (name) and EMAIL_USER (address)
                                  - OTHER person names         → PERSON_1, PERSON_2, ...
                                  - OTHER email addresses      → EMAIL_1, EMAIL_2, ...
                                  - Company / org names        → ORG_1, ORG_2, ...
                                  - Project / product          → PROJECT_1, PROJECT_2, ...

                                Respond with a JSON object matching this exact shape:

                                  {
                                    "redacted": "<the full text of every relevant inbox
                                                  email, verbatim, with sensitive data
                                                  replaced by tokens. Preserve all
                                                  paragraph breaks, dates, dollar
                                                  amounts, quoted text, and original
                                                  wording. Append a single trailing
                                                  paragraph stating: 'The user
                                                  (PERSON_USER, EMAIL_USER) is asking
                                                  for help drafting a reply about: ...'>",
                                    "tokens": {
                                      "PERSON_USER": "{{UserProfile.Name}}",
                                      "EMAIL_USER":  "{{UserProfile.Email}}",
                                      "PERSON_1":    "Bob Martinez",
                                      "EMAIL_1":     "bob.martinez@acme-corp.com",
                                      "ORG_1":       "Acme Corp",
                                      ...
                                    }
                                  }

                                Rules:
                                  - The 'redacted' field MUST be the full email body,
                                    not a summary. If the inbox has 3 emails, include
                                    all 3, each separated by '---'.
                                  - The 'redacted' field MUST contain NO original
                                    names, emails, companies, or projects — only the
                                    placeholder tokens.
                                  - The 'tokens' field MUST cover EVERY token that
                                    appears in 'redacted'.
                                  - The 'tokens' field MUST always include
                                    PERSON_USER → "{{UserProfile.Name}}" and
                                    EMAIL_USER → "{{UserProfile.Email}}", even if those
                                    tokens don't appear in the redacted text.
                                """,
                        },
                    });
            });

        // 2. Cloud reply writer — sees ONLY tokens. No tools, no private data.
        builder.AddAIAgent(
            name: "cloud-reply-writer",
            instructions: """
                You are a senior email drafter working in the cloud. You will be
                given a redacted email plus a one-line summary of what the user
                wants. Personal data has already been replaced with placeholder
                tokens like PERSON_1, ORG_2, PERSON_USER, etc.

                Rules:
                  - KEEP every token EXACTLY as written. Do not invent new
                    tokens, do not guess at names, do not de-tokenise.
                  - Address the recipient using the same token that the
                    incoming email's 'From:' line used (e.g. "Hi PERSON_1,").
                  - Sign the reply with PERSON_USER (the user is the sender
                    of the reply you are drafting):

                        Best regards,
                        PERSON_USER

                  - Be concise. 4-6 sentences. Acknowledge, commit, close.

                Output ONLY the draft reply as Markdown.
                """,
            description: "Cloud-side reply drafter — only ever sees redacted, tokenised content.",
            chatClientServiceKey: AIModels.Cloud);

        // 3. Finisher — local. The rehydrate adapter has already replaced the
        //    tokens, so this agent's job is purely to make the reply read
        //    naturally (tone, flow, light polish — no PII handling).
        builder.AddAIAgent(
            name: "local-email-finisher",
            instructions: """
                You are the on-device finisher. You will be given a draft email
                reply that already has the real names, emails, companies, and
                projects filled in.

                Your job is to lightly polish the wording for tone and flow.
                Do NOT add new facts. Do NOT change names, dates, or numbers.
                Output ONLY the final polished reply as Markdown — no
                commentary, no preamble, no extra text.
                """,
            description: "On-device finisher: polishes the rehydrated reply for tone and flow.",
            chatClientServiceKey: AIModels.Local);

        // Wire by hand using WorkflowBuilder. Plain agents are bound via
        // BindAsExecutor (so streaming, chat bubbles, and traces work). Tiny
        // adapter executors between agents move structured state out of the
        // conversation and into IWorkflowContext, so rehydration is
        // mechanical rather than LLM-guessed.
        builder.AddWorkflow("local-cloud-email-triage", (sp, key) =>
        {
            var redactorAgent    = sp.GetRequiredKeyedService<AIAgent>("local-email-redactor");
            var replyWriterAgent = sp.GetRequiredKeyedService<AIAgent>("cloud-reply-writer");
            var finisherAgent    = sp.GetRequiredKeyedService<AIAgent>("local-email-finisher");

            var hostOpts = new AIAgentHostOptions
            {
                ReassignOtherAgentsAsUsers = true,
                ForwardIncomingMessages = true,
            };

            ExecutorBinding redactor          = redactorAgent.BindAsExecutor(hostOpts);
            ExecutorBinding storeRedactionMap = new StoreRedactionMapAdapter();
            ExecutorBinding replyWriter       = replyWriterAgent.BindAsExecutor(hostOpts);
            ExecutorBinding rehydrate         = new RehydrateAdapter();
            ExecutorBinding finisher          = finisherAgent.BindAsExecutor(hostOpts);
            ExecutorBinding output            = new OutputMessagesExecutor();

            return new WorkflowBuilder(redactor)
                .AddEdge(redactor, storeRedactionMap)
                .AddEdge(storeRedactionMap, replyWriter)
                .AddEdge(replyWriter, rehydrate)
                .AddEdge(rehydrate, finisher)
                .AddEdge(finisher, output)
                .WithOutputFrom(output)
                .WithName(key)
                .WithDescription(
                    "On-device RAG → on-device redaction → cloud reply → " +
                    "deterministic rehydration → on-device polish. The cloud " +
                    "agent only ever sees placeholder tokens; rehydration is " +
                    "a literal text substitution from a map stored in workflow " +
                    "state, not an LLM guess.")
                .Build();
        }).AddAsAIAgent();
    }
}
