namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// A simple directed graph definition for rendering.
/// </summary>
public sealed record GraphDefinition(
    IReadOnlyList<GraphNodeDef> Nodes,
    IReadOnlyList<GraphEdgeDef> Edges);

/// <summary>
/// A node in the graph with an id, label, and optional description.
/// </summary>
public sealed record GraphNodeDef(
    string Id,
    string Label,
    string? Description = null,
    GraphNodeShape Shape = GraphNodeShape.RoundedRect);

/// <summary>
/// A directed edge from source to target.
/// </summary>
public sealed record GraphEdgeDef(
    string SourceId,
    string TargetId,
    string? Label = null,
    GraphEdgeStyle Style = GraphEdgeStyle.Solid);

public enum GraphNodeShape { RoundedRect, Stadium, Diamond }
public enum GraphEdgeStyle { Solid, Dashed, Dotted }
