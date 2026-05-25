namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// Draws edge lines and arrows between workflow nodes on a GraphicsView overlay.
/// Uses bezier curves for a polished look.
/// </summary>
internal class GraphEdgeDrawable : IDrawable
{
    private const double NodeWidth = 140;
    private const double NodeHeight = 48;
    private const double Padding = 10;
    private const double ArrowSize = 7;

    public GraphLayout? Layout { get; set; }
    public bool IsDarkMode { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Layout is null || Layout.Edges.Count == 0) return;

        var strokeColor = IsDarkMode
            ? Color.FromArgb("#9090b8")
            : Color.FromArgb("#7070a0");

        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var edge in Layout.Edges)
        {
            var fromNode = Layout.Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
            var toNode = Layout.Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
            if (fromNode is null || toNode is null) continue;

            // Use different colors for different edge types
            var edgeColor = edge.IsBidirectional
                ? Color.FromArgb(IsDarkMode ? "#8B5CF6" : "#7C3AED")
                : edge.Label == "route"
                    ? Color.FromArgb(IsDarkMode ? "#F59E0B" : "#D97706")
                    : strokeColor;

            canvas.StrokeColor = edgeColor;

            var fromCenterX = (float)(fromNode.X + Padding + NodeWidth / 2);
            var fromCenterY = (float)(fromNode.Y + Padding + NodeHeight / 2);
            var toCenterX = (float)(toNode.X + Padding + NodeWidth / 2);
            var toCenterY = (float)(toNode.Y + Padding + NodeHeight / 2);

            var (startX, startY) = GetBorderPoint(fromCenterX, fromCenterY, toCenterX, toCenterY);
            var (endX, endY) = GetBorderPoint(toCenterX, toCenterY, fromCenterX, fromCenterY);

            // Determine if this is a loopback edge (target above source)
            if (edge.IsBidirectional || toNode.Y < fromNode.Y)
            {
                DrawCurvedEdge(canvas, startX, startY, endX, endY, isLoopback: true);
            }
            else if (Math.Abs(fromCenterX - toCenterX) > 10)
            {
                // Non-vertical edge: draw bezier curve
                DrawCurvedEdge(canvas, startX, startY, endX, endY, isLoopback: false);
            }
            else
            {
                // Straight vertical edge
                canvas.DrawLine(startX, startY, endX, endY);
            }

            // Draw arrowhead
            DrawArrow(canvas, startX, startY, endX, endY, edgeColor);

            // Draw label if present
            if (!string.IsNullOrEmpty(edge.Label))
            {
                var midX = (startX + endX) / 2;
                var midY = (startY + endY) / 2;
                canvas.FontSize = 9;
                canvas.FontColor = edgeColor;
                canvas.DrawString(edge.Label, midX - 20, midY - 12, 40, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }

    private static void DrawCurvedEdge(ICanvas canvas, float startX, float startY, float endX, float endY, bool isLoopback)
    {
        var path = new PathF();
        path.MoveTo(startX, startY);

        if (isLoopback)
        {
            // Loopback curve: arc to the right of the nodes
            var offsetX = 60f;
            var cp1X = startX + offsetX;
            var cp1Y = startY;
            var cp2X = endX + offsetX;
            var cp2Y = endY;
            path.CurveTo(cp1X, cp1Y, cp2X, cp2Y, endX, endY);
        }
        else
        {
            // Standard bezier: control points create a smooth S-curve
            var midY = (startY + endY) / 2;
            path.CurveTo(startX, midY, endX, midY, endX, endY);
        }

        canvas.DrawPath(path);
    }

    private static (float x, float y) GetBorderPoint(float cx, float cy, float targetX, float targetY)
    {
        var dx = targetX - cx;
        var dy = targetY - cy;
        var halfW = (float)(NodeWidth / 2);
        var halfH = (float)(NodeHeight / 2);

        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return (cx, cy);

        var scaleX = Math.Abs(dx) > 0 ? halfW / Math.Abs(dx) : float.MaxValue;
        var scaleY = Math.Abs(dy) > 0 ? halfH / Math.Abs(dy) : float.MaxValue;
        var scale = Math.Min(scaleX, scaleY);

        return (cx + dx * scale, cy + dy * scale);
    }

    private static void DrawArrow(ICanvas canvas, float startX, float startY, float endX, float endY, Color fillColor)
    {
        var angle = (float)Math.Atan2(endY - startY, endX - startX);
        var sin = (float)Math.Sin(angle);
        var cos = (float)Math.Cos(angle);

        var p1X = endX - (float)ArrowSize * cos + (float)(ArrowSize * 0.5) * sin;
        var p1Y = endY - (float)ArrowSize * sin - (float)(ArrowSize * 0.5) * cos;
        var p2X = endX - (float)ArrowSize * cos - (float)(ArrowSize * 0.5) * sin;
        var p2Y = endY - (float)ArrowSize * sin + (float)(ArrowSize * 0.5) * cos;

        var path = new PathF();
        path.MoveTo(endX, endY);
        path.LineTo(p1X, p1Y);
        path.LineTo(p2X, p2Y);
        path.Close();

        canvas.FillColor = fillColor;
        canvas.FillPath(path);
    }
}
