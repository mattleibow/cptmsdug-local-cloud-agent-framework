using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Demo.Orchestrations.Tools;

namespace Demo.Orchestrations;

public static class SequentialWorkflow
{
    public static void AddSequentialWorkflow(this IHostApplicationBuilder builder)
    {
        // Reporter has a SearchNews tool to research stories
        builder.AddAIAgent("sequential-newsdesk-reporter", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            instructions: """
                You are a news reporter. Research the given topic using the SearchNews tool,
                then write a concise news article (250 words) based on the results.
                Include a headline, lead paragraph, and supporting details. Use journalistic style.
                Always call SearchNews first before writing.
                """,
            tools: [AIFunctionFactory.Create(NewsDeskTools.SearchNews)]
        ));

        // Factchecker has a VerifyFact tool to check claims
        builder.AddAIAgent("sequential-newsdesk-factchecker", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            instructions: """
                You are a fact-checker. Review the article and use the VerifyFact tool to check
                key claims and statistics. Add [VERIFIED] or [NEEDS SOURCE] annotations based on
                tool results. Call VerifyFact for at least 2-3 major claims in the article.
                """,
            tools: [AIFunctionFactory.Create(NewsDeskTools.VerifyFact)]
        ));

        // Editor has a FormatForPublication tool
        builder.AddAIAgent("sequential-newsdesk-editor", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            instructions: """
                You are a senior editor. Polish the article for clarity and flow.
                Ensure the headline is compelling. Once the article is ready, use the
                FormatForPublication tool to produce the final formatted output.
                """,
            tools: [AIFunctionFactory.Create(NewsDeskTools.FormatForPublication)]
        ));

        builder.AddWorkflow("sequential-newsdesk", (sp, key) =>
        {
            var agents = new[] {
                    "sequential-newsdesk-reporter",
                    "sequential-newsdesk-factchecker",
                    "sequential-newsdesk-editor"
                }
                .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                .ToArray();
            return AgentWorkflowBuilder.BuildSequential(key, agents);
        }).AddAsAIAgent();
    }
}
