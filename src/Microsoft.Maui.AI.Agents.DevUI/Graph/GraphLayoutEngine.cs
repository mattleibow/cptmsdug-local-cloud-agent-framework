namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// Simplified Sugiyama-style layered layout engine.
///   1) Layer assignment via longest-path topological levels (cycles handled by ignoring back-edges).
///   2) Within-layer ordering (insertion order kept; barycenter sweep would go here for crossings).
///   3) Node positioning: layers stacked vertically, nodes spread horizontally within a layer.
///   4) Edge routing: straight line from source's appropriate face to target's. Back-edges loop
///      around the right side so GroupChat ring-style loopbacks are visible.
/// </summary>
public sealed class GraphLayoutEngine
{
    public double NodeWidth { get; init; } = 140;
    public double NodeHeight { get; init; } = 48;
    public double HGap { get; init; } = 40;
    public double VGap { get; init; } = 60;
    public double Padding { get; init; } = 24;

    public GraphLayoutResult Layout(GraphDefinition graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Nodes.Count == 0)
        {
            return new GraphLayoutResult
            {
                Nodes = new Dictionary<string, GraphNodeLayout>(),
                Edges = Array.Empty<GraphEdgeLayout>(),
                Width = Padding * 2,
                Height = Padding * 2,
            };
        }

        // Build adjacency and detect back-edges via DFS coloring.
        var nodeIds = graph.Nodes.Select(n => n.Id).ToList();
        var outgoing = nodeIds.ToDictionary(id => id, _ => new List<string>());
        foreach (var e in graph.Edges)
        {
            if (outgoing.ContainsKey(e.SourceId) && outgoing.ContainsKey(e.TargetId))
                outgoing[e.SourceId].Add(e.TargetId);
        }

        var backEdges = DetectBackEdges(nodeIds, outgoing);

        // Forward adjacency excluding back-edges, used for layer assignment.
        var forwardOut = nodeIds.ToDictionary(id => id, _ => new List<string>());
        foreach (var e in graph.Edges)
        {
            if (!forwardOut.ContainsKey(e.SourceId) || !forwardOut.ContainsKey(e.TargetId))
                continue;
            if (backEdges.Contains((e.SourceId, e.TargetId)))
                continue;
            forwardOut[e.SourceId].Add(e.TargetId);
        }

        // Compute layers via longest-path from any source.
        var layer = AssignLayers(nodeIds, forwardOut);

        // Group nodes by layer in original order to keep stable ordering.
        var byLayer = new SortedDictionary<int, List<string>>();
        foreach (var id in nodeIds)
        {
            if (!byLayer.TryGetValue(layer[id], out var list))
                byLayer[layer[id]] = list = new List<string>();
            list.Add(id);
        }

        // Compute width of each layer and the overall width.
        var maxNodesInLayer = byLayer.Values.Max(l => l.Count);
        var layerWidth = maxNodesInLayer * NodeWidth + (maxNodesInLayer - 1) * HGap;
        var totalWidth = layerWidth + Padding * 2;

        // Position nodes: center each layer horizontally.
        var positions = new Dictionary<string, GraphNodeLayout>();
        double y = Padding;
        foreach (var (layerIndex, ids) in byLayer)
        {
            var count = ids.Count;
            var rowWidth = count * NodeWidth + (count - 1) * HGap;
            var startX = Padding + (layerWidth - rowWidth) / 2;
            for (var i = 0; i < count; i++)
            {
                var x = startX + i * (NodeWidth + HGap);
                positions[ids[i]] = new GraphNodeLayout(ids[i], x, y, NodeWidth, NodeHeight, layerIndex);
            }
            y += NodeHeight + VGap;
        }
        var totalHeight = y - VGap + Padding;

        // Route edges.
        var edges = new List<GraphEdgeLayout>(graph.Edges.Count);
        foreach (var e in graph.Edges)
        {
            if (!positions.TryGetValue(e.SourceId, out var src) || !positions.TryGetValue(e.TargetId, out var dst))
                continue;

            IReadOnlyList<(double X, double Y)> waypoints;
            if (backEdges.Contains((e.SourceId, e.TargetId)))
            {
                // Loopback: out the right side, down/up around, into the right side of target.
                var loopX = totalWidth - Padding / 2;
                waypoints = new (double X, double Y)[]
                {
                    (src.Right, src.CenterY),
                    (loopX, src.CenterY),
                    (loopX, dst.CenterY),
                    (dst.Right, dst.CenterY),
                };
            }
            else if (Math.Abs(src.CenterX - dst.CenterX) < 0.5)
            {
                // Straight vertical.
                waypoints = new (double X, double Y)[]
                {
                    (src.CenterX, src.Bottom),
                    (dst.CenterX, dst.Y),
                };
            }
            else
            {
                // Diagonal with a small vertical stub for nicer arrowhead alignment.
                var midY = (src.Bottom + dst.Y) / 2;
                waypoints = new (double X, double Y)[]
                {
                    (src.CenterX, src.Bottom),
                    (src.CenterX, midY),
                    (dst.CenterX, midY),
                    (dst.CenterX, dst.Y),
                };
            }

            edges.Add(new GraphEdgeLayout(e.SourceId, e.TargetId, waypoints, e.Label, e.Style));
        }

        return new GraphLayoutResult
        {
            Nodes = positions,
            Edges = edges,
            Width = totalWidth,
            Height = totalHeight,
        };
    }

    private static HashSet<(string, string)> DetectBackEdges(
        IList<string> nodeIds,
        IDictionary<string, List<string>> outgoing)
    {
        const int White = 0, Gray = 1, Black = 2;
        var color = nodeIds.ToDictionary(id => id, _ => White);
        var backEdges = new HashSet<(string, string)>();

        void Visit(string u)
        {
            color[u] = Gray;
            foreach (var v in outgoing[u])
            {
                if (color[v] == Gray)
                    backEdges.Add((u, v));
                else if (color[v] == White)
                    Visit(v);
            }
            color[u] = Black;
        }

        foreach (var id in nodeIds)
            if (color[id] == White)
                Visit(id);

        return backEdges;
    }

    private static Dictionary<string, int> AssignLayers(
        IList<string> nodeIds,
        IDictionary<string, List<string>> forwardOut)
    {
        // Compute in-degree on the DAG (back-edges removed).
        var inDeg = nodeIds.ToDictionary(id => id, _ => 0);
        foreach (var (src, dsts) in forwardOut)
            foreach (var d in dsts)
                inDeg[d]++;

        var layer = nodeIds.ToDictionary(id => id, _ => 0);
        var queue = new Queue<string>(inDeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var v in forwardOut[u])
            {
                if (layer[v] < layer[u] + 1)
                    layer[v] = layer[u] + 1;
                if (--inDeg[v] == 0)
                    queue.Enqueue(v);
            }
        }
        return layer;
    }
}
