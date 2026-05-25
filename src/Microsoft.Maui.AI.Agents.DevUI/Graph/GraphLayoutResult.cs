namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// Result of running the graph layout engine: absolute positions for nodes
/// and waypoint polylines for edges.
/// </summary>
public sealed class GraphLayoutResult
{
    public required IReadOnlyDictionary<string, GraphNodeLayout> Nodes { get; init; }
    public required IReadOnlyList<GraphEdgeLayout> Edges { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}

/// <summary>
/// Computed bounds for a node.
/// </summary>
public sealed record GraphNodeLayout(string Id, double X, double Y, double Width, double Height, int Layer)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>
/// Computed routing for an edge as a list of waypoints (at least 2: source and target).
/// </summary>
public sealed record GraphEdgeLayout(
    string SourceId,
    string TargetId,
    IReadOnlyList<(double X, double Y)> Waypoints,
    string? Label,
    GraphEdgeStyle Style);
