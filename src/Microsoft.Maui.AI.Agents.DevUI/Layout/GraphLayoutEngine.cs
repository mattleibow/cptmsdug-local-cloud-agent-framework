namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// BFS-based graph layout engine that positions workflow nodes
/// according to the orchestration topology. Uses compact vertical
/// layouts suitable for narrow sidebar panels.
/// </summary>
internal static class GraphLayoutEngine
{
    private const double NodeWidth = 160;
    private const double NodeHeight = 56;
    private const double HGap = 20;
    private const double VGap = 28;

    public static GraphLayout ComputeLayout(WorkflowInfo workflow)
    {
        return workflow.Kind switch
        {
            OrchestrationKind.Sequential => LayoutSequential(workflow),
            OrchestrationKind.Concurrent => LayoutConcurrent(workflow),
            OrchestrationKind.Handoff => LayoutHandoff(workflow),
            OrchestrationKind.GroupChat => LayoutGroupChat(workflow),
            _ => LayoutSequential(workflow)
        };
    }

    /// <summary>
    /// Sequential: vertical chain A ↓ B ↓ C
    /// </summary>
    private static GraphLayout LayoutSequential(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        for (var i = 0; i < workflow.Executors.Count; i++)
        {
            var exec = workflow.Executors[i];
            nodes.Add(new LayoutNode
            {
                Id = exec.Id,
                Name = exec.Name,
                X = 0,
                Y = i * (NodeHeight + VGap)
            });

            if (i > 0)
            {
                edges.Add(new LayoutEdge
                {
                    SourceId = workflow.Executors[i - 1].Id,
                    TargetId = exec.Id
                });
            }
        }

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth,
            Height = nodes.Count * (NodeHeight + VGap) - VGap
        };
    }

    /// <summary>
    /// Concurrent: parallel column then merger below.
    /// Layout:
    ///   [A]
    ///   [B]     (parallel analysts stacked)
    ///   [C]
    ///   ---
    ///   [merger] (below with extra gap)
    /// </summary>
    private static GraphLayout LayoutConcurrent(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        if (workflow.Executors.Count == 0)
            return new GraphLayout { Nodes = nodes, Edges = edges };

        var parallelCount = workflow.Executors.Count - 1;
        var merger = workflow.Executors[^1];

        for (var i = 0; i < parallelCount; i++)
        {
            var exec = workflow.Executors[i];
            nodes.Add(new LayoutNode
            {
                Id = exec.Id,
                Name = exec.Name,
                X = 0,
                Y = i * (NodeHeight + VGap)
            });

            edges.Add(new LayoutEdge
            {
                SourceId = exec.Id,
                TargetId = merger.Id
            });
        }

        // Merger below with extra spacing
        var mergerY = parallelCount * (NodeHeight + VGap) + 10;
        nodes.Add(new LayoutNode
        {
            Id = merger.Id,
            Name = merger.Name,
            X = 0,
            Y = mergerY
        });

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth,
            Height = mergerY + NodeHeight
        };
    }

    /// <summary>
    /// Handoff: triage at top, specialists below indented.
    /// Layout:
    ///  [triage]
    ///    [specialist-1]
    ///    [specialist-2]
    ///    [specialist-3]
    /// </summary>
    private static GraphLayout LayoutHandoff(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        if (workflow.Executors.Count == 0)
            return new GraphLayout { Nodes = nodes, Edges = edges };

        var triage = workflow.Executors[0];
        var specialists = workflow.Executors.Skip(1).ToList();

        // Triage at top
        nodes.Add(new LayoutNode
        {
            Id = triage.Id,
            Name = triage.Name,
            X = 0,
            Y = 0
        });

        // Specialists below, slightly indented
        var indent = 20.0;
        for (var i = 0; i < specialists.Count; i++)
        {
            var spec = specialists[i];
            nodes.Add(new LayoutNode
            {
                Id = spec.Id,
                Name = spec.Name,
                X = indent,
                Y = (i + 1) * (NodeHeight + VGap)
            });

            edges.Add(new LayoutEdge
            {
                SourceId = triage.Id,
                TargetId = spec.Id,
                Label = spec.Name
            });
        }

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth + indent,
            Height = (specialists.Count + 1) * (NodeHeight + VGap) - VGap
        };
    }

    /// <summary>
    /// Group Chat: vertical list with round-robin edges indicating cycling.
    /// </summary>
    private static GraphLayout LayoutGroupChat(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        var count = workflow.Executors.Count;
        if (count == 0)
            return new GraphLayout { Nodes = nodes, Edges = edges };

        for (var i = 0; i < count; i++)
        {
            var exec = workflow.Executors[i];
            nodes.Add(new LayoutNode
            {
                Id = exec.Id,
                Name = exec.Name,
                X = 0,
                Y = i * (NodeHeight + VGap)
            });
        }

        // Round-robin edges
        for (var i = 0; i < count; i++)
        {
            edges.Add(new LayoutEdge
            {
                SourceId = workflow.Executors[i].Id,
                TargetId = workflow.Executors[(i + 1) % count].Id,
                IsBidirectional = true
            });
        }

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth,
            Height = count * (NodeHeight + VGap) - VGap
        };
    }
}

/// <summary>
/// Result of graph layout computation.
/// </summary>
internal sealed class GraphLayout
{
    public IReadOnlyList<LayoutNode> Nodes { get; init; } = [];
    public IReadOnlyList<LayoutEdge> Edges { get; init; } = [];
    public double Width { get; init; }
    public double Height { get; init; }
}

/// <summary>
/// A positioned node in the layout.
/// </summary>
internal sealed class LayoutNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

/// <summary>
/// A connection edge in the layout.
/// </summary>
internal sealed class LayoutEdge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public string? Label { get; init; }
    public bool IsBidirectional { get; init; }
}
