using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.Layered;
using MsaglNode = Microsoft.Msagl.Core.Layout.Node;
using MsaglEdge = Microsoft.Msagl.Core.Layout.Edge;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// Graph layout engine using MSAGL (Microsoft Automatic Graph Layout)
/// for proper layered/Sugiyama graph positioning.
/// </summary>
internal static class GraphLayoutEngine
{
    private const double NodeWidth = 140;
    private const double NodeHeight = 48;
    private const double Padding = 20;

    public static GraphLayout ComputeLayout(WorkflowInfo workflow)
    {
        if (workflow.Executors.Count == 0)
            return new GraphLayout { Nodes = [], Edges = [] };

        var graph = new GeometryGraph();
        var msaglNodes = new Dictionary<string, MsaglNode>();

        // Create MSAGL nodes
        foreach (var exec in workflow.Executors)
        {
            var node = new MsaglNode(CurveFactory.CreateRectangle(NodeWidth, NodeHeight, new Microsoft.Msagl.Core.Geometry.Point(0, 0)), exec.Id);
            graph.Nodes.Add(node);
            msaglNodes[exec.Id] = node;
        }

        // Create edges based on workflow topology
        var edgeMeta = new List<(string src, string tgt, string? label, bool bidir)>();

        switch (workflow.Kind)
        {
            case OrchestrationKind.Sequential:
                BuildSequentialEdges(workflow, edgeMeta);
                break;
            case OrchestrationKind.Concurrent:
                BuildConcurrentEdges(workflow, edgeMeta);
                break;
            case OrchestrationKind.Handoff:
                BuildHandoffEdges(workflow, edgeMeta);
                break;
            case OrchestrationKind.GroupChat:
                BuildGroupChatEdges(workflow, edgeMeta);
                break;
            default:
                BuildSequentialEdges(workflow, edgeMeta);
                break;
        }

        // Add edges to MSAGL graph
        foreach (var (src, tgt, _, _) in edgeMeta)
        {
            if (msaglNodes.TryGetValue(src, out var srcNode) && msaglNodes.TryGetValue(tgt, out var tgtNode))
            {
                var edge = new MsaglEdge(srcNode, tgtNode);
                graph.Edges.Add(edge);
            }
        }

        // Run Sugiyama layered layout
        var settings = new SugiyamaLayoutSettings
        {
            LayerSeparation = 60,
            NodeSeparation = 40,
            EdgeRoutingSettings = { EdgeRoutingMode = Microsoft.Msagl.Core.Routing.EdgeRoutingMode.Spline }
        };

        var layout = new LayeredLayout(graph, settings);
        layout.Run();

        // Extract positioned nodes (translate so min is at Padding)
        var minX = graph.Nodes.Min(n => n.Center.X - NodeWidth / 2);
        var minY = graph.Nodes.Min(n => n.Center.Y - NodeHeight / 2);

        var layoutNodes = new List<LayoutNode>();
        foreach (var exec in workflow.Executors)
        {
            var msaglNode = msaglNodes[exec.Id];
            layoutNodes.Add(new LayoutNode
            {
                Id = exec.Id,
                Name = exec.Name,
                X = msaglNode.Center.X - NodeWidth / 2 - minX + Padding,
                Y = msaglNode.Center.Y - NodeHeight / 2 - minY + Padding
            });
        }

        // Convert edge metadata to layout edges
        var layoutEdges = edgeMeta.Select(e => new LayoutEdge
        {
            SourceId = e.src,
            TargetId = e.tgt,
            Label = e.label,
            IsBidirectional = e.bidir
        }).ToList();

        var maxX = layoutNodes.Max(n => n.X) + NodeWidth + Padding;
        var maxY = layoutNodes.Max(n => n.Y) + NodeHeight + Padding;

        return new GraphLayout
        {
            Nodes = layoutNodes,
            Edges = layoutEdges,
            Width = maxX,
            Height = maxY
        };
    }

    private static void BuildSequentialEdges(WorkflowInfo workflow, List<(string, string, string?, bool)> edges)
    {
        for (var i = 0; i < workflow.Executors.Count - 1; i++)
            edges.Add((workflow.Executors[i].Id, workflow.Executors[i + 1].Id, null, false));
    }

    private static void BuildConcurrentEdges(WorkflowInfo workflow, List<(string, string, string?, bool)> edges)
    {
        if (workflow.Executors.Count < 2) return;

        // Last executor is the merger; all others fan into it
        var merger = workflow.Executors[^1];
        for (var i = 0; i < workflow.Executors.Count - 1; i++)
            edges.Add((workflow.Executors[i].Id, merger.Id, null, false));
    }

    private static void BuildHandoffEdges(WorkflowInfo workflow, List<(string, string, string?, bool)> edges)
    {
        if (workflow.Executors.Count < 2) return;

        // First executor is dispatcher; rest are specialists
        var dispatcher = workflow.Executors[0];
        for (var i = 1; i < workflow.Executors.Count; i++)
            edges.Add((dispatcher.Id, workflow.Executors[i].Id, "route", false));
    }

    private static void BuildGroupChatEdges(WorkflowInfo workflow, List<(string, string, string?, bool)> edges)
    {
        var count = workflow.Executors.Count;
        if (count < 2) return;

        // Chain forward
        for (var i = 0; i < count - 1; i++)
            edges.Add((workflow.Executors[i].Id, workflow.Executors[i + 1].Id, null, false));

        // Loopback from last to first
        edges.Add((workflow.Executors[^1].Id, workflow.Executors[0].Id, "next round", true));
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
