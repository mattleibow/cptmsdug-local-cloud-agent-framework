using Microsoft.Maui.Controls.Shapes;
using MShapes = Microsoft.Maui.Controls.Shapes;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

// NOTE: These types are compile-only stubs preserved so that the legacy
// AgentDevUIView (which is scheduled to be re-wired onto the new
// Microsoft.Maui.AI.Agents.DevUI.Graph.GraphView) continues to build.
// The runtime behaviour is intentionally empty.

public sealed class WorkflowGraphLayout : Microsoft.Maui.Controls.Layout
{
    public Microsoft.Maui.AI.Agents.DevUI.WorkflowInfo? Workflow { get; set; }

    public object? ComputedLayout { get; set; }

    public int EdgeCount { get; set; }

    protected override Microsoft.Maui.Layouts.ILayoutManager CreateLayoutManager()
        => new StubLayoutManager(this);

    private sealed class StubLayoutManager : Microsoft.Maui.Layouts.ILayoutManager
    {
        private readonly WorkflowGraphLayout _owner;
        public StubLayoutManager(WorkflowGraphLayout owner) => _owner = owner;

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            foreach (var child in _owner)
                child.Measure(widthConstraint, heightConstraint);
            return Size.Zero;
        }

        public Size ArrangeChildren(Rect bounds)
        {
            foreach (var child in _owner)
                child.Arrange(bounds);
            return bounds.Size;
        }
    }
}

public static class EdgePathBuilder
{
    public static IList<MShapes.Path> CreateEdgePaths(object layout, bool isDarkMode)
    {
        _ = layout;
        _ = isDarkMode;
        return Array.Empty<MShapes.Path>();
    }
}
