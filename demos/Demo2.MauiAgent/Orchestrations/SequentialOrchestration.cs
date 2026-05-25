using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.Orchestrations;

/// <summary>
/// Sequential orchestration: Agents execute one after another in a defined order.
/// Theme: Story Publishing Pipeline (Writer → Editor → Publisher)
/// </summary>
public class SequentialOrchestration
{
    public string Name => "Sequential: Story Pipeline";
    public string Description => "Writer drafts → Editor refines → Publisher formats";
    public OrchestrationKind Kind => OrchestrationKind.Sequential;

    public IReadOnlyList<AgentDefinition> Agents { get; } =
    [
        new("writer", "You write short stories (300 words or less) about the specified topic."),
        new("editor", "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing."),
        new("publisher", "You take the final story and format it with a catchy headline and a brief teaser.")
    ];

    public IReadOnlyList<AITool> GetTools()
    {
        return [AIFunctionFactory.Create(FormatStory)];
    }

    [Description("Formats the story for publication with title and body.")]
    private static string FormatStory(string title, string story) => $"""
        # {title}

        {story}
        """;
}

public static class SequentialOrchestrationExtensions
{
    public static IServiceCollection AddSequentialWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<SequentialOrchestration>();
        return services;
    }
}
