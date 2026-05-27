using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 2 of the meeting-invite pipeline. Runs on-device.
///
/// Reads the picked customer email and produces a SHORT prose brief
/// — 3-4 sentences — capturing what the cloud needs to draft a useful
/// meeting invite:
///
///   • Who the customer is (full name, so the invite can address them)
///   • What identifier the issue is attached to (order ID, account ID, etc.)
///   • What the issue actually is and what they're asking for
///
/// The agent is explicitly told to OMIT sensitive PII that the cloud has
/// no business seeing: physical addresses, phone numbers, passwords,
/// credit-card / bank / SSN numbers. We keep the brief plain text — no
/// JSON, no list of tokens — so there's nothing for the constrained
/// decoder to mishandle.
/// </summary>
public static class LocalIssueSummariserAgentExtensions
{
    public static IHostApplicationBuilder AddLocalIssueSummariserAgent(
        this IHostApplicationBuilder builder, string name)
    {
        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description =
                """
                On-device issue summariser: distills the picked email down to
                a short prose brief the cloud can use to draft a meeting
                invite — keeps the customer name and order/account IDs,
                drops addresses / phones / passwords / cards / SSNs.
                """,
            ChatOptions = new ChatOptions
            {
                MaxOutputTokens = 400,
                Instructions = """
                    You are an on-device customer-support assistant. Read the
                    customer email and write a SHORT brief (3-4 sentences,
                    plain prose) for a colleague who will reply.

                    Your brief MUST include, when present in the email:
                      - The customer's full name (so the reply can address them)
                      - Any order / invoice / account / case ID they mention
                      - The issue itself and what the customer is asking for

                    Your brief MUST NEVER include:
                      - The customer's physical address
                      - Phone numbers
                      - Passwords, login credentials, security questions
                      - Credit-card or bank-account numbers
                      - Social-security or other government ID numbers

                    These get stripped because the colleague writing the reply
                    works in the cloud — they should never see this kind of
                    personal data.

                    Output ONLY the brief — no preamble, no headings, no
                    bullet list, no commentary. Just 3-4 plain sentences.
                    """,
            },
        };

        builder.AddAIAgent(name, (sp, key) => new ChatClientAgent(
            sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
            options));

        return builder;
    }
}
