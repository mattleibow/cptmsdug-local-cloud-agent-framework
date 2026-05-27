using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 4 of the email-triage pipeline. Runs in the cloud (Azure OpenAI).
///
/// Receives only the colleague's first name, the user's first name, the
/// subject, and the token-redacted body — never any last names, email
/// addresses, company names, project names, or dollar amounts. Drafts a
/// plain reply body that re-uses the placeholder tokens so the on-device
/// assembler can rehydrate them.
/// </summary>
public static class CloudReplyWriterAgentExtensions
{
    public static IHostApplicationBuilder AddCloudReplyWriterAgent(
        this IHostApplicationBuilder builder, string name)
    {
        builder.AddAIAgent(
            name: name,
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
        return builder;
    }
}

