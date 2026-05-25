using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

namespace Demo.Orchestrations;

public static class SequentialWorkflow
{
    public static void AddSequentialWorkflow(this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent("sequential-newsdesk-reporter",
            "You are a news reporter. Write a concise news article (250 words) about the given topic. Include a headline, lead paragraph, and supporting details. Use journalistic style.");
        builder.AddAIAgent("sequential-newsdesk-factchecker",
            "You are a fact-checker. Review the article for accuracy, flag any unsupported claims, and add [VERIFIED] or [NEEDS SOURCE] annotations. Suggest corrections where needed.");
        builder.AddAIAgent("sequential-newsdesk-editor",
            "You are a senior editor. Polish the article for clarity and flow. Ensure the headline is compelling. Format the final version ready for publication.");

        builder.AddWorkflow("sequential-newsdesk", (sp, key) =>
        {
            var agents = new[] { "sequential-newsdesk-reporter", "sequential-newsdesk-factchecker", "sequential-newsdesk-editor" }
                .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                .ToArray();
            return AgentWorkflowBuilder.BuildSequential(key, agents);
        }).AddAsAIAgent();
    }
}
