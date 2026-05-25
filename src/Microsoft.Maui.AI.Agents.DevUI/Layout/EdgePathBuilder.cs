using Microsoft.Maui.Controls.Shapes;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// Creates MAUI Path shapes for workflow graph edges.
/// Each edge becomes a Path view with a computed PathGeometry
/// (bezier curves + arrowhead).
/// </summary>
internal static class EdgePathBuilder
{
    private const double NodeWidth = 140;
    private const double NodeHeight = 48;
    private const double ArrowSize = 8;

    private static readonly Color DefaultEdgeColor = Color.FromArgb("#7070a0");
    private static readonly Color DefaultEdgeColorDark = Color.FromArgb("#9090b8");
    private static readonly Color RouteColor = Color.FromArgb("#D97706");
    private static readonly Color RouteColorDark = Color.FromArgb("#F59E0B");
    private static readonly Color BiDirColor = Color.FromArgb("#7C3AED");
    private static readonly Color BiDirColorDark = Color.FromArgb("#8B5CF6");

    /// <summary>
    /// Creates Path views for all edges in the computed layout.
    /// </summary>
    public static List<Microsoft.Maui.Controls.Shapes.Path> CreateEdgePaths(GraphLayout layout, bool isDarkMode)
    {
        var paths = new List<Microsoft.Maui.Controls.Shapes.Path>();

        foreach (var edge in layout.Edges)
        {
            var fromNode = layout.Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
            var toNode = layout.Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
            if (fromNode is null || toNode is null) continue;

            var edgeColor = GetEdgeColor(edge, isDarkMode);
            var path = CreateEdgePath(fromNode, toNode, edge, edgeColor);
            paths.Add(path);
        }

        return paths;
    }

    private static Color GetEdgeColor(LayoutEdge edge, bool isDarkMode)
    {
        if (edge.IsBidirectional)
            return isDarkMode ? BiDirColorDark : BiDirColor;
        if (edge.Label == "route")
            return isDarkMode ? RouteColorDark : RouteColor;
        return isDarkMode ? DefaultEdgeColorDark : DefaultEdgeColor;
    }

    private static Microsoft.Maui.Controls.Shapes.Path CreateEdgePath(
        LayoutNode fromNode, LayoutNode toNode, LayoutEdge edge, Color color)
    {
        var fromCenterX = fromNode.X + NodeWidth / 2;
        var fromCenterY = fromNode.Y + NodeHeight / 2;
        var toCenterX = toNode.X + NodeWidth / 2;
        var toCenterY = toNode.Y + NodeHeight / 2;

        // Compute connection points on node borders
        var (startX, startY) = GetBorderPoint(fromCenterX, fromCenterY, toCenterX, toCenterY);
        var (endX, endY) = GetBorderPoint(toCenterX, toCenterY, fromCenterX, fromCenterY);

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(startX, startY) };

        // Determine curve type
        bool isLoopback = edge.IsBidirectional || toNode.Y < fromNode.Y;

        if (isLoopback)
        {
            // Loopback: curve to the right
            var offsetX = 50.0;
            var cp1 = new Point(startX + offsetX, startY);
            var cp2 = new Point(endX + offsetX, endY);
            figure.Segments.Add(new BezierSegment
            {
                Point1 = cp1,
                Point2 = cp2,
                Point3 = new Point(endX, endY)
            });
        }
        else if (Math.Abs(fromCenterX - toCenterX) > 10)
        {
            // Non-vertical: smooth S-curve
            var midY = (startY + endY) / 2;
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(startX, midY),
                Point2 = new Point(endX, midY),
                Point3 = new Point(endX, endY)
            });
        }
        else
        {
            // Straight vertical line
            figure.Segments.Add(new LineSegment { Point = new Point(endX, endY) });
        }

        geometry.Figures.Add(figure);

        // Add arrowhead as a separate figure
        var arrowFigure = CreateArrowhead(startX, startY, endX, endY);
        geometry.Figures.Add(arrowFigure);

        var path = new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(color),
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            InputTransparent = true
        };

        return path;
    }

    private static PathFigure CreateArrowhead(double startX, double startY, double endX, double endY)
    {
        var angle = Math.Atan2(endY - startY, endX - startX);
        var sin = Math.Sin(angle);
        var cos = Math.Cos(angle);

        var p1X = endX - ArrowSize * cos + ArrowSize * 0.4 * sin;
        var p1Y = endY - ArrowSize * sin - ArrowSize * 0.4 * cos;
        var p2X = endX - ArrowSize * cos - ArrowSize * 0.4 * sin;
        var p2Y = endY - ArrowSize * sin + ArrowSize * 0.4 * cos;

        var figure = new PathFigure
        {
            StartPoint = new Point(endX, endY),
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new LineSegment { Point = new Point(p1X, p1Y) });
        figure.Segments.Add(new LineSegment { Point = new Point(p2X, p2Y) });

        return figure;
    }

    private static (double x, double y) GetBorderPoint(double cx, double cy, double targetX, double targetY)
    {
        var dx = targetX - cx;
        var dy = targetY - cy;
        var halfW = NodeWidth / 2;
        var halfH = NodeHeight / 2;

        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return (cx, cy);

        var scaleX = Math.Abs(dx) > 0 ? halfW / Math.Abs(dx) : double.MaxValue;
        var scaleY = Math.Abs(dy) > 0 ? halfH / Math.Abs(dy) : double.MaxValue;
        var scale = Math.Min(scaleX, scaleY);

        return (cx + dx * scale, cy + dy * scale);
    }
}
