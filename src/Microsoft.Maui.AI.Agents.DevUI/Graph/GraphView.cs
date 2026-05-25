using Microsoft.Maui.Controls.Shapes;
using MShapes = Microsoft.Maui.Controls.Shapes;

namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// Top-level workflow graph control. Lays out node + edge MAUI views inside a
/// custom layout, with pan + pinch-zoom on the outer container.
/// </summary>
public sealed class GraphView : ContentView
{
    private const double MinScale = 0.3;
    private const double MaxScale = 3.0;

    private readonly GraphViewLayout _surface;
    private readonly GraphLayoutEngine _engine = new();

    private double _panStartX;
    private double _panStartY;
    private double _pinchStartScale = 1;
    private Point _pinchOrigin;
    private double _pinchOriginTx;
    private double _pinchOriginTy;

    public static readonly BindableProperty GraphProperty =
        BindableProperty.Create(
            nameof(Graph),
            typeof(GraphDefinition),
            typeof(GraphView),
            propertyChanged: OnGraphChanged);

    public GraphDefinition? Graph
    {
        get => (GraphDefinition?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public GraphView()
    {
        _surface = new GraphViewLayout
        {
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            AnchorX = 0,
            AnchorY = 0,
        };

        Content = _surface;
        IsClippedToBounds = true;
        BackgroundColor = Color.FromArgb("#0F0D1A");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);
    }

    private static void OnGraphChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GraphView gv)
            gv.Rebuild();
    }

    private void Rebuild()
    {
        _surface.Clear();
        _surface.ClearBounds();

        var graph = Graph;
        if (graph is null || graph.Nodes.Count == 0)
        {
            _surface.WidthRequest = 0;
            _surface.HeightRequest = 0;
            return;
        }

        var result = _engine.Layout(graph);

        // Edges first so they appear under nodes.
        foreach (var edge in result.Edges)
        {
            var path = BuildEdgePath(edge);
            _surface.Add(path);
            _surface.SetBounds(path, new Rect(0, 0, result.Width, result.Height));

            var arrow = BuildArrowhead(edge);
            _surface.Add(arrow);
            _surface.SetBounds(arrow, new Rect(0, 0, result.Width, result.Height));
        }

        foreach (var node in graph.Nodes)
        {
            if (!result.Nodes.TryGetValue(node.Id, out var pos))
                continue;
            var view = new GraphNodeView { Text = node.Label };
            _surface.Add(view);
            _surface.SetBounds(view, new Rect(pos.X, pos.Y, pos.Width, pos.Height));
        }

        _surface.SetContentSize(new Size(result.Width, result.Height));
        _surface.WidthRequest = result.Width;
        _surface.HeightRequest = result.Height;
        _surface.InvalidateMeasure();
    }

    private static MShapes.Path BuildEdgePath(GraphEdgeLayout edge)
    {
        var pts = edge.Waypoints;
        var figure = new PathFigure { StartPoint = new Point(pts[0].X, pts[0].Y) };

        if (pts.Count == 2)
        {
            // Two points: use a cubic bezier with control points offset vertically
            var p0 = pts[0];
            var p1 = pts[1];
            var midY = (p0.Y + p1.Y) / 2;
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(p0.X, midY),
                Point2 = new Point(p1.X, midY),
                Point3 = new Point(p1.X, p1.Y),
            });
        }
        else
        {
            // Multiple waypoints: connect with bezier curves through midpoints
            for (var i = 1; i < pts.Count - 1; i++)
            {
                var prev = pts[i - 1];
                var curr = pts[i];
                var next = pts[i + 1];
                var midX = (curr.X + next.X) / 2;
                var midY = (curr.Y + next.Y) / 2;
                figure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(prev.X, curr.Y),
                    Point2 = new Point(curr.X, curr.Y),
                    Point3 = new Point(midX, midY),
                });
            }
            // Final segment to last point
            var last = pts[^1];
            var secondLast = pts[^2];
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(secondLast.X, last.Y),
                Point2 = new Point(last.X, last.Y),
                Point3 = new Point(last.X, last.Y),
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var path = new MShapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(Color.FromArgb("#7A6FE8")),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Colors.Transparent),
            InputTransparent = true,
        };

        switch (edge.Style)
        {
            case GraphEdgeStyle.Dashed:
                path.StrokeDashArray = new DoubleCollection { 6, 4 };
                break;
            case GraphEdgeStyle.Dotted:
                path.StrokeDashArray = new DoubleCollection { 2, 3 };
                break;
        }
        return path;
    }

    private static MShapes.Path BuildArrowhead(GraphEdgeLayout edge)
    {
        var n = edge.Waypoints.Count;
        var p1 = edge.Waypoints[n - 2];
        var p2 = edge.Waypoints[n - 1];

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001) len = 1;
        var ux = dx / len;
        var uy = dy / len;

        const double size = 8;
        var baseX = p2.X - ux * size;
        var baseY = p2.Y - uy * size;
        var px = -uy;
        var py = ux;

        var tip = new Point(p2.X, p2.Y);
        var left = new Point(baseX + px * (size / 2), baseY + py * (size / 2));
        var right = new Point(baseX - px * (size / 2), baseY - py * (size / 2));

        var figure = new PathFigure { StartPoint = tip, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment { Point = left });
        figure.Segments.Add(new LineSegment { Point = right });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new MShapes.Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.FromArgb("#7A6FE8")),
            Stroke = new SolidColorBrush(Color.FromArgb("#7A6FE8")),
            StrokeThickness = 1,
            InputTransparent = true,
        };
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = _surface.TranslationX;
                _panStartY = _surface.TranslationY;
                break;
            case GestureStatus.Running:
                _surface.TranslationX = _panStartX + e.TotalX;
                _surface.TranslationY = _panStartY + e.TotalY;
                break;
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinchStartScale = _surface.Scale;
                _pinchOrigin = e.ScaleOrigin;
                _pinchOriginTx = _surface.TranslationX;
                _pinchOriginTy = _surface.TranslationY;
                break;
            case GestureStatus.Running:
            {
                var newScale = Math.Clamp(_pinchStartScale * e.Scale, MinScale, MaxScale);
                // Zoom around the pinch origin (in this view's coordinate space).
                var originX = _pinchOrigin.X * Width;
                var originY = _pinchOrigin.Y * Height;
                var ratio = newScale / _pinchStartScale;
                _surface.Scale = newScale;
                _surface.TranslationX = originX - (originX - _pinchOriginTx) * ratio;
                _surface.TranslationY = originY - (originY - _pinchOriginTy) * ratio;
                break;
            }
        }
    }

    /// <summary>Resets pan and zoom to their defaults.</summary>
    public void ResetView()
    {
        _surface.Scale = 1;
        _surface.TranslationX = 0;
        _surface.TranslationY = 0;
    }
}
