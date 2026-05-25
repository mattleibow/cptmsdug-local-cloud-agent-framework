using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// Layout manager that positions nodes according to GraphLayoutEngine output.
/// First child is expected to be a GraphicsView (edge overlay), remaining children are nodes.
/// </summary>
internal class WorkflowGraphLayoutManager : ILayoutManager
{
    private const double NodeWidth = 140;
    private const double NodeHeight = 48;
    private const double Padding = 10;

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
            return new Size(NodeWidth + Padding * 2, NodeHeight + Padding * 2);

        // Measure each child so they report their desired size
        foreach (var child in _layout.Children.Where(c => c.Visibility == Visibility.Visible))
            child.Measure(widthConstraint, heightConstraint);

        return new Size(
            computed.Width + Padding * 2,
            computed.Height + Padding * 2);
    }

    public Size ArrangeChildren(Rect bounds)
    {
        var computed = _layout.ComputedLayout;
        if (computed is null) return bounds.Size;

        var children = _layout.Children.Where(c => c.Visibility == Visibility.Visible).ToList();
        if (children.Count == 0) return bounds.Size;

        // First child is the GraphicsView overlay — fill entire area
        var firstChild = children[0];
        if (firstChild is IView graphicsView)
        {
            graphicsView.Arrange(new Rect(0, 0, computed.Width + Padding * 2, computed.Height + Padding * 2));
        }

        // Remaining children are node views — position according to computed layout
        var nodeChildren = children.Skip(1).ToList();
        for (var i = 0; i < nodeChildren.Count && i < computed.Nodes.Count; i++)
        {
            var layoutNode = computed.Nodes[i];
            var nodeView = nodeChildren[i];
            nodeView.Arrange(new Rect(
                layoutNode.X + Padding,
                layoutNode.Y + Padding,
                NodeWidth,
                NodeHeight));
        }

        return new Size(computed.Width + Padding * 2, computed.Height + Padding * 2);
    }
}
