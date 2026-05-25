using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// Layout manager that positions edge Path shapes and node views according to
/// the MSAGL-computed layout. Children order: [EdgePaths...] [NodeViews...]
/// Edge Paths are arranged to fill the full canvas so their geometry is in absolute coords.
/// Node views are positioned at their computed (X, Y).
/// </summary>
internal class WorkflowGraphLayoutManager : ILayoutManager
{
    private const double NodeWidth = 140;
    private const double NodeHeight = 48;

    private readonly WorkflowGraphLayout _layout;

    public WorkflowGraphLayoutManager(WorkflowGraphLayout layout)
    {
        _layout = layout;
    }

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        _layout.RecomputeLayout();
        var computed = _layout.ComputedLayout;

        if (computed is null || computed.Nodes.Count == 0)
            return new Size(NodeWidth + 40, NodeHeight + 40);

        // Measure each child so they report their desired size
        foreach (var child in _layout.Children.Where(c => c.Visibility == Visibility.Visible))
            child.Measure(widthConstraint, heightConstraint);

        return new Size(computed.Width, computed.Height);
    }

    public Size ArrangeChildren(Rect bounds)
    {
        var computed = _layout.ComputedLayout;
        if (computed is null) return bounds.Size;

        var children = _layout.Children.Where(c => c.Visibility == Visibility.Visible).ToList();
        if (children.Count == 0) return bounds.Size;

        var edgeCount = _layout.EdgeCount;
        var canvasWidth = computed.Width;
        var canvasHeight = computed.Height;

        // Edge Paths fill the full canvas area (their PathGeometry is in absolute coordinates)
        for (var i = 0; i < edgeCount && i < children.Count; i++)
        {
            children[i].Arrange(new Rect(0, 0, canvasWidth, canvasHeight));
        }

        // Node views are positioned at computed layout positions
        var nodeChildren = children.Skip(edgeCount).ToList();
        for (var i = 0; i < nodeChildren.Count && i < computed.Nodes.Count; i++)
        {
            var layoutNode = computed.Nodes[i];
            nodeChildren[i].Arrange(new Rect(
                layoutNode.X,
                layoutNode.Y,
                NodeWidth,
                NodeHeight));
        }

        return new Size(canvasWidth, canvasHeight);
    }
}
