using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Models;
using Demo.MauiAgentApp.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 1 of the meeting-invite pipeline. Runs on-device.
///
/// Uses a <c>TextSearchProvider</c> backed by <see cref="InboxService"/> to
/// pull a fresh batch of fabricated customer emails (the user's PRIVATE
/// inbox) and select the ONE most relevant to the user's request. Returns
/// the chosen email as a structured <see cref="PickedEmail"/> so the
/// downstream stages can work against typed fields.
///
/// This is the LOCAL RAG step. The inbox content never leaves the device —
/// only the summary produced by stage 2 does.
/// </summary>
public static class LocalInboxSearchAgentExtensions
{
    public static IHostApplicationBuilder AddLocalInboxSearchAgent(
        this IHostApplicationBuilder builder, string name)
    {
        builder.AddAIAgent(
            name,
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
                            """
                            ## Inbox candidates
                            Each entry below is a separate email from the user's inbox.
                            Pick exactly ONE — the most relevant to the user's request.
                            """,
                        CitationsPrompt = string.Empty,
                    });

                var options = new ChatClientAgentOptions
                {
                    Name = key,
                    Description =
                        """
                        On-device inbox search: chooses the single inbox email most
                        relevant to the user's request and returns it as structured
                        JSON for the downstream summariser.
                        """,
                    AIContextProviders = [ragProvider],
                    ChatOptions = new ChatOptions
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<PickedEmail>(),
                        // Bound output so a confused Apple Intelligence run can't
                        // loop forever streaming the entire RAG context into the
                        // body field. 1500 tokens comfortably fits a single
                        // ~3-paragraph body plus the JSON wrapper.
                        MaxOutputTokens = 1500,
                        Instructions = """
                            You are an on-device inbox searcher. The context lists
                            the user's inbox emails, each with these labels:

                              SENDER_EMAIL, SENDER_NAME,
                              SUBJECT, RECEIVED, body

                            Pick exactly ONE entry. Copy the fields from THAT
                            entry — and ONLY that entry — into the schema:

                              SENDER_EMAIL → senderEmail
                              SENDER_NAME  → senderName
                              SUBJECT      → subject
                              body lines   → body

                            Never combine fields from multiple entries. Never
                            stitch bodies together. Stop after one entry.
                            """,
                    },
                };

                return new ChatClientAgent(
                    sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                    options).WithTelemetry();
            });

        return builder;
    }
}
