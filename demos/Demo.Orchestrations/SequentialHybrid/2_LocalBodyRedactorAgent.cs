using Demo.Orchestrations.SequentialHybrid.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 2 of the email-triage pipeline. Runs on-device.
///
/// Reads the picked email body and returns four bounded typed lists of
/// substrings spotted in it (last names, companies, projects, dollar
/// amounts). Does NOT rewrite the body — the deterministic substitution
/// happens in stage 3 (<see cref="CloudPromptAdapterExecutor"/>).
/// </summary>
public static class LocalBodyRedactorAgentExtensions
{
    public static IHostApplicationBuilder AddLocalBodyRedactorAgent(this IHostApplicationBuilder builder, string name)
    {
        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description =
                """
                On-device entity spotter: scans the picked email body for last
                names, company names, project names, and dollar amounts and
                returns them as a structured list for the next executor to
                tokenise.
                """,
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<RedactedBody>(),
                // Generous token budget so the trailing list never gets
                // guillotined mid-string. The [MaxLength(5)] cap on each
                // list is the real bound on output size; this just gives
                // the constrained decoder room to fill four full arrays
                // plus all the JSON syntax around them.
                MaxOutputTokens = 1000,
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
        };

        builder.AddAIAgent(name, (sp, key) => new ChatClientAgent(
            sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
            options));

        return builder;
    }
}

