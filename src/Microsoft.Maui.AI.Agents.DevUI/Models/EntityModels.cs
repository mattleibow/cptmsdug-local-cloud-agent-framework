namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// Describes a registered agent that can handle chat conversations.
/// </summary>
public class AgentInfo
{
    /// <summary>Unique identifier for the agent.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Short description of what the agent does.</summary>
    public string? Description { get; init; }

    /// <summary>System prompt / instructions for the agent.</summary>
    public string? Instructions { get; init; }

    /// <summary>Names of tools available to this agent.</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>Model identifier if known.</summary>
    public string? ModelId { get; init; }
}

/// <summary>
/// Describes a registered workflow (orchestration of multiple agents).
/// </summary>
public class WorkflowInfo
{
    /// <summary>Unique identifier for the workflow.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Short description of what the workflow does.</summary>
    public string? Description { get; init; }

    /// <summary>The type of orchestration pattern.</summary>
    public OrchestrationKind Kind { get; init; }

    /// <summary>Ordered list of executor/agent definitions in this workflow.</summary>
    public IReadOnlyList<ExecutorInfo> Executors { get; init; } = [];

    /// <summary>Edge groups describing the graph topology.</summary>
    public IReadOnlyList<EdgeGroup> EdgeGroups { get; init; } = [];

    /// <summary>ID of the starting executor.</summary>
    public string? StartExecutorId { get; init; }
}

/// <summary>
/// Describes an executor (agent node) within a workflow.
/// </summary>
public class ExecutorInfo
{
    /// <summary>Unique identifier within the workflow.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Short description of what this executor does.</summary>
    public string? Description { get; init; }

    /// <summary>System prompt for this executor.</summary>
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// Describes connections between executors in a workflow graph.
/// </summary>
public class EdgeGroup
{
    /// <summary>Type of edge group determining the topology.</summary>
    public required EdgeGroupType Type { get; init; }

    /// <summary>Individual edges in this group.</summary>
    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

/// <summary>
/// A single directed edge between two executors.
/// </summary>
public class Edge
{
    /// <summary>Source executor ID.</summary>
    public required string SourceId { get; init; }

    /// <summary>Target executor ID.</summary>
    public required string TargetId { get; init; }

    /// <summary>Optional condition label for conditional edges.</summary>
    public string? Condition { get; init; }
}

/// <summary>
/// Types of edge groups matching the Agent Framework graph model.
/// </summary>
public enum EdgeGroupType
{
    /// <summary>Single direct edge (A to B).</summary>
    Single,

    /// <summary>Fan-out: one source to multiple targets (parallel dispatch).</summary>
    FanOut,

    /// <summary>Fan-in: multiple sources converge to one target.</summary>
    FanIn
}

/// <summary>
/// Types of orchestration patterns.
/// </summary>
public enum OrchestrationKind
{
    /// <summary>Agents execute one after another.</summary>
    Sequential,

    /// <summary>Agents execute in parallel.</summary>
    Concurrent,

    /// <summary>Agents transfer control based on context.</summary>
    Handoff,

    /// <summary>Agents collaborate in a shared conversation.</summary>
    GroupChat
}
