namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// Graph layout engine that positions workflow nodes according to
/// the orchestration topology. Designed for a 280-300px wide panel.
/// </summary>
internal static class GraphLayoutEngine
{
    private const double NodeWidth = 160;
    private const double NodeHeight = 56;
    private const double HGap = 24;
    private const double VGap = 32;
    private const double Padding = 12;

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
    /// Sequential: vertical chain centered
    ///   [A]
    ///    |
    ///   [B]
    ///    |
    ///   [C]
    /// </summary>
    private static GraphLayout LayoutSequential(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();
        var centerX = Padding;

        for (var i = 0; i < workflow.Executors.Count; i++)
        {
            var exec = workflow.Executors[i];
            nodes.Add(new LayoutNode
            {
                Id = exec.Id,
                Name = exec.Name,
                X = centerX,
                Y = Padding + i * (NodeHeight + VGap)
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
            Width = NodeWidth + Padding * 2,
            Height = Padding * 2 + nodes.Count * (NodeHeight + VGap) - VGap
        };
    }

    /// <summary>
    /// Concurrent: fan-out from implicit start, parallel nodes side-by-side,
    /// then fan-in to merger.
    ///
    ///      [A] [B] [C]    (parallel, side-by-side or stacked if narrow)
    ///        \  |  /
    ///       [merger]
    ///
    /// With 300px width and 3 nodes: stack vertically with fan-in arrows.
    /// </summary>
    private static GraphLayout LayoutConcurrent(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        if (workflow.Executors.Count == 0)
            return new GraphLayout { Nodes = nodes, Edges = edges };

        var parallelCount = workflow.Executors.Count - 1;
        var merger = workflow.Executors[^1];

        // Can we fit side-by-side? Each node needs NodeWidth + HGap
        var sideByWidth = parallelCount * NodeWidth + (parallelCount - 1) * HGap + Padding * 2;
        var useSideBySide = sideByWidth <= 300 && parallelCount <= 3;

        if (useSideBySide)
        {
            // Horizontal arrangement for parallel nodes
            var startX = Padding;
            for (var i = 0; i < parallelCount; i++)
            {
                var exec = workflow.Executors[i];
                nodes.Add(new LayoutNode
                {
                    Id = exec.Id,
                    Name = exec.Name,
                    X = startX + i * (NodeWidth + HGap),
                    Y = Padding
                });
                edges.Add(new LayoutEdge { SourceId = exec.Id, TargetId = merger.Id });
            }

            // Merger centered below
            var mergerX = startX + (parallelCount - 1) * (NodeWidth + HGap) / 2.0;
            nodes.Add(new LayoutNode
            {
                Id = merger.Id,
                Name = merger.Name,
                X = mergerX,
                Y = Padding + NodeHeight + VGap * 1.5
            });

            return new GraphLayout
            {
                Nodes = nodes,
                Edges = edges,
                Width = sideByWidth,
                Height = Padding * 2 + 2 * NodeHeight + VGap * 1.5
            };
        }
        else
        {
            // Vertical stack with fan-in indicator
            for (var i = 0; i < parallelCount; i++)
            {
                var exec = workflow.Executors[i];
                nodes.Add(new LayoutNode
                {
                    Id = exec.Id,
                    Name = exec.Name,
                    X = Padding,
                    Y = Padding + i * (NodeHeight + VGap * 0.6)
                });
                edges.Add(new LayoutEdge { SourceId = exec.Id, TargetId = merger.Id });
            }

            var mergerY = Padding + parallelCount * (NodeHeight + VGap * 0.6) + VGap * 0.5;
            nodes.Add(new LayoutNode
            {
                Id = merger.Id,
                Name = merger.Name,
                X = Padding,
                Y = mergerY
            });

            return new GraphLayout
            {
                Nodes = nodes,
                Edges = edges,
                Width = NodeWidth + Padding * 2,
                Height = mergerY + NodeHeight + Padding
            };
        }
    }

    /// <summary>
    /// Handoff: dispatcher at top, branching arrows to specialists.
    ///
    ///       [dispatcher]
    ///      /     |      \
    ///   [net] [soft] [hard]
    ///
    /// If narrow, specialists stack vertically with indent.
    /// </summary>
    private static GraphLayout LayoutHandoff(WorkflowInfo workflow)
    {
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        if (workflow.Executors.Count == 0)
            return new GraphLayout { Nodes = nodes, Edges = edges };

        var triage = workflow.Executors[0];
        var specialists = workflow.Executors.Skip(1).ToList();

        // Dispatcher centered at top
        nodes.Add(new LayoutNode
        {
            Id = triage.Id,
            Name = triage.Name,
            X = Padding,
            Y = Padding
        });

        // Specialists below with indent to show branching
        var indent = 24.0;
        for (var i = 0; i < specialists.Count; i++)
        {
            var spec = specialists[i];
            nodes.Add(new LayoutNode
            {
                Id = spec.Id,
                Name = spec.Name,
                X = Padding + indent,
                Y = Padding + (i + 1) * (NodeHeight + VGap)
            });

            edges.Add(new LayoutEdge
            {
                SourceId = triage.Id,
                TargetId = spec.Id,
                Label = "route"
            });
        }

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth + indent + Padding * 2,
            Height = Padding * 2 + (specialists.Count + 1) * (NodeHeight + VGap) - VGap
        };
    }

    /// <summary>
    /// Group Chat: circular-style arrangement. Nodes in a column with
    /// bidirectional arrows indicating round-robin discussion.
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
                X = Padding,
                Y = Padding + i * (NodeHeight + VGap)
            });
        }

        // Forward and loopback edges
        for (var i = 0; i < count - 1; i++)
        {
            edges.Add(new LayoutEdge
            {
                SourceId = workflow.Executors[i].Id,
                TargetId = workflow.Executors[i + 1].Id
            });
        }

        // Loopback from last to first (indicates rounds)
        edges.Add(new LayoutEdge
        {
            SourceId = workflow.Executors[^1].Id,
            TargetId = workflow.Executors[0].Id,
            IsBidirectional = true,
            Label = "next round"
        });

        return new GraphLayout
        {
            Nodes = nodes,
            Edges = edges,
            Width = NodeWidth + Padding * 2,
            Height = Padding * 2 + count * (NodeHeight + VGap) - VGap
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
