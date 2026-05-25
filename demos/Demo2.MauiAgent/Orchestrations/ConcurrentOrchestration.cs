using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.Orchestrations;

/// <summary>
/// Concurrent orchestration: Agents execute in parallel, results are merged.
/// Theme: Research Briefing (multiple analysts research different aspects simultaneously)
/// </summary>
public class ConcurrentOrchestration
{
    public string Name => "Concurrent: Research Briefing";
    public string Description => "Multiple analysts research in parallel, then merge findings";
    public OrchestrationKind Kind => OrchestrationKind.Concurrent;

    public IReadOnlyList<AgentDefinition> Agents { get; } =
    [
        new("technical-analyst", "You are a technical analyst. Analyze the technical aspects, feasibility, and implementation details of the given topic. Keep analysis to 150 words."),
        new("market-analyst", "You are a market analyst. Analyze the market opportunity, competition, and business potential of the given topic. Keep analysis to 150 words."),
        new("risk-analyst", "You are a risk analyst. Identify potential risks, challenges, and mitigation strategies for the given topic. Keep analysis to 150 words."),
        new("synthesizer", "You are a synthesis expert. Take multiple analysis reports and combine them into a coherent executive briefing of 200 words or less.")
    ];
}

public static class ConcurrentOrchestrationExtensions
{
    public static IServiceCollection AddConcurrentWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<ConcurrentOrchestration>();
        return services;
    }
}
