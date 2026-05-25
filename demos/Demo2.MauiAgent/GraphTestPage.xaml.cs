using Microsoft.Maui.AI.Agents.DevUI.Graph;

namespace Demo2.MauiAgent;

public partial class GraphTestPage : ContentPage
{
    public GraphTestPage()
    {
        InitializeComponent();

        SequentialGraph.Graph = BuildSequential();
        ConcurrentGraph.Graph = BuildConcurrent();
        HandoffGraph.Graph = BuildHandoff();
        GroupChatGraph.Graph = BuildGroupChat();
    }

    private static GraphDefinition BuildSequential() => new(
        new[]
        {
            new GraphNodeDef("A", "Step A"),
            new GraphNodeDef("B", "Step B"),
            new GraphNodeDef("C", "Step C"),
        },
        new[]
        {
            new GraphEdgeDef("A", "B"),
            new GraphEdgeDef("B", "C"),
        });

    private static GraphDefinition BuildConcurrent() => new(
        new[]
        {
            new GraphNodeDef("A", "Worker A"),
            new GraphNodeDef("B", "Worker B"),
            new GraphNodeDef("C", "Worker C"),
            new GraphNodeDef("D", "Merger"),
        },
        new[]
        {
            new GraphEdgeDef("A", "D"),
            new GraphEdgeDef("B", "D"),
            new GraphEdgeDef("C", "D"),
        });

    private static GraphDefinition BuildHandoff() => new(
        new[]
        {
            new GraphNodeDef("D", "Dispatcher"),
            new GraphNodeDef("S1", "Specialist 1"),
            new GraphNodeDef("S2", "Specialist 2"),
            new GraphNodeDef("S3", "Specialist 3"),
        },
        new[]
        {
            new GraphEdgeDef("D", "S1"),
            new GraphEdgeDef("D", "S2"),
            new GraphEdgeDef("D", "S3"),
        });

    private static GraphDefinition BuildGroupChat() => new(
        new[]
        {
            new GraphNodeDef("P1", "Participant 1"),
            new GraphNodeDef("P2", "Participant 2"),
            new GraphNodeDef("P3", "Participant 3"),
        },
        new[]
        {
            new GraphEdgeDef("P1", "P2"),
            new GraphEdgeDef("P2", "P3"),
            new GraphEdgeDef("P3", "P1", Style: GraphEdgeStyle.Dashed),
        });
}
