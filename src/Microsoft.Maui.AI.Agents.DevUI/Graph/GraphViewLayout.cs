using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// A custom Layout that arranges children at absolute positions assigned via
/// <see cref="SetBounds"/>. Children with no assigned bounds are hidden at (0,0).
/// </summary>
public sealed class GraphViewLayout : Microsoft.Maui.Controls.Layout
{
    private readonly Dictionary<IView, Rect> _bounds = new();
    private Size _contentSize;

    public void SetBounds(IView view, Rect bounds)
    {
        _bounds[view] = bounds;
    }

    public void SetContentSize(Size size)
    {
        _contentSize = size;
    }

    public void ClearBounds()
    {
        _bounds.Clear();
        _contentSize = Size.Zero;
    }

    protected override ILayoutManager CreateLayoutManager() => new GraphViewLayoutManager(this);

    internal IReadOnlyDictionary<IView, Rect> NodeBounds => _bounds;
    internal Size ContentSize => _contentSize;
}

internal sealed class GraphViewLayoutManager : ILayoutManager
{
    private readonly GraphViewLayout _layout;

    public GraphViewLayoutManager(GraphViewLayout layout)
    {
        _layout = layout;
    }

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        foreach (var child in _layout)
        {
            if (_layout.NodeBounds.TryGetValue(child, out var rect))
                child.Measure(rect.Width, rect.Height);
            else
                child.Measure(double.PositiveInfinity, double.PositiveInfinity);
        }

        var size = _layout.ContentSize;
        return new Size(
            double.IsFinite(widthConstraint) ? Math.Max(size.Width, 0) : size.Width,
            double.IsFinite(heightConstraint) ? Math.Max(size.Height, 0) : size.Height);
    }

    public Size ArrangeChildren(Rect bounds)
    {
        foreach (var child in _layout)
        {
            if (_layout.NodeBounds.TryGetValue(child, out var rect))
                child.Arrange(rect);
            else
                child.Arrange(new Rect(0, 0, 0, 0));
        }
        return _layout.ContentSize;
    }
}
