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
    public static void AddSequentialWorkflow(
        this IHostApplicationBuilder builder)
    {
        var newsTools = NewsDeskToolContext.Default.Tools;

        builder.AddAIAgent(
            "sequential-newsdesk-reporter",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "News reporter that researches topics and writes articles.",
                instructions: """
                    You are a news reporter. ALWAYS call the search_news tool FIRST to
                    gather sources. Then write a concise news article (200 words) that
                    incorporates AT LEAST 3
                    specific claims from the search results — include the statistics, percentages,
                    dollar amounts, and named quotes verbatim. Use journalistic style with
                    a headline,
                    lead paragraph, and supporting details. Do NOT hedge or rewrite
                    specific numbers —
                    keep them exactly as the source provided so the fact-checker can verify them.
                    """,
                tools: [.. newsTools.Where(
                    t => t.Name == "search_news")]
            ));

        builder.AddAIAgent(
            "sequential-newsdesk-factchecker",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Fact-checker that verifies claims in articles.",
                instructions: """
                    You are a fact-checker. You MUST call the verify_fact tool for AT LEAST 3
                    specific claims in the article (statistics, percentages, dollar amounts, named
                    quotes, dates). Extract each claim verbatim and pass it to
                    verify_fact. After all
                    checks, write a fact-check report listing each claim with its VERIFIED /
                    PARTIALLY
                    VERIFIED / UNVERIFIED / DISPUTED status and the reasoning. Flag any claims that
                    should be removed or rewritten before publication.
                    """,
                tools: [.. newsTools.Where(
                    t => t.Name == "verify_fact")]
            ));

        builder.AddAIAgent(
            "sequential-newsdesk-editor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description:
                    "Senior editor that polishes articles for publication.",
                instructions: """
                    You are a senior editor. Read the article and the fact-checker's
                    report. Remove or
                    rewrite any UNVERIFIED or DISPUTED claims. Polish the article for
                    clarity and flow,
                    ensure the headline is compelling, then call the format_for_publication tool to
                    produce the final formatted output.
                    """,
                tools: [.. newsTools.Where(
                    t => t.Name == "format_for_publication")]
            ));

        builder.AddWorkflow("sequential-newsdesk", (sp, key) =>
        {
            var agents = new[]
            {
                "sequential-newsdesk-reporter",
                "sequential-newsdesk-factchecker",
                "sequential-newsdesk-editor"
            }
            .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
            .ToArray();
            var workflow = AgentWorkflowBuilder
                .BuildSequential(key, agents);
            workflow.SetDescription(
                "A reporter researches, a fact-checker verifies, and an editor publishes.");
            return workflow;
        }).AddAsAIAgent();
    }
}
