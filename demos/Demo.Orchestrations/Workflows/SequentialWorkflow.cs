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
        var newsTools = NewsDeskToolContext.Default.Tools;

        builder.AddAIAgent("sequential-newsdesk-reporter", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "News reporter that researches topics and writes articles.",
            instructions: """
                You are a news reporter. Research the given topic using the search_news tool,
                then write a concise news article (250 words) based on the results.
                Include a headline, lead paragraph, and supporting details. Use journalistic style.
                Always call search_news first before writing.
                """,
            tools: [.. newsTools.Where(t => t.Name == "search_news")]
        ));

        builder.AddAIAgent("sequential-newsdesk-factchecker", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Fact-checker that verifies claims in articles.",
            instructions: """
                You are a fact-checker. Review the article and use the verify_fact tool to check
                key claims and statistics. Add [VERIFIED] or [NEEDS SOURCE] annotations based on
                tool results. Call verify_fact for at least 2-3 major claims in the article.
                """,
            tools: [.. newsTools.Where(t => t.Name == "verify_fact")]
        ));

        builder.AddAIAgent("sequential-newsdesk-editor", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Senior editor that polishes articles for publication.",
            instructions: """
                You are a senior editor. Polish the article for clarity and flow.
                Ensure the headline is compelling. Once the article is ready, use the
                format_for_publication tool to produce the final formatted output.
                """,
            tools: [.. newsTools.Where(t => t.Name == "format_for_publication")]
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
            var workflow = AgentWorkflowBuilder.BuildSequential(key, agents);
            workflow.SetDescription("A reporter researches, a fact-checker verifies, and an editor publishes.");
            return workflow;
        }).AddAsAIAgent();
    }
}
