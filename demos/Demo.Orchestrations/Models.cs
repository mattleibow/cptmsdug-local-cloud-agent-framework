using Microsoft.Extensions.AI;

namespace Demo.Orchestrations;

/// <summary>
/// Defines the shared orchestration patterns and agent configurations
/// used across both the web (MAF DevUI) and native (MAUI DevUI) demos.
/// </summary>
public enum OrchestrationKind
{
    Sequential,
    Concurrent,
    Handoff,
    GroupChat
}

/// <summary>
/// An agent definition with a name and system prompt.
/// </summary>
public record AgentDefinition(string Name, string SystemPrompt);

/// <summary>
/// A workflow definition with agents organized in a specific orchestration pattern.
/// </summary>
public record WorkflowDefinition(
    string Id,
    string Name,
    string Description,
    OrchestrationKind Kind,
    string DemoPrompt,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<AITool>? Tools = null);
