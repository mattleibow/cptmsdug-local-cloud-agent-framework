using Demo.Orchestrations.SequentialHybrid.Models;
using Demo.Orchestrations.SequentialHybrid.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 1 of the email-triage pipeline. Runs on-device.
///
/// Uses a <c>TextSearchProvider</c> backed by <see cref="InboxService"/> to
/// pull a fresh batch of fabricated inbox emails, picks the ONE entry most
/// relevant to the user's request, and returns it as a structured
/// <see cref="PickedEmail"/>.
/// </summary>
public static class LocalInboxPickerAgentExtensions
{
    public static IHostApplicationBuilder AddLocalInboxPickerAgent(this IHostApplicationBuilder builder, string name)
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

                return new ChatClientAgent(
                    sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                    new ChatClientAgentOptions
                    {
                        Name = key,
                        Description =
                            """
                            On-device inbox picker: chooses the single inbox email most
                            relevant to the user's request and returns it as structured JSON.
                            """,
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
        return builder;
    }
}

