namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// Draws edge lines and arrows between workflow nodes on a GraphicsView overlay.
/// </summary>
internal class GraphEdgeDrawable : IDrawable
{
    private const double NodeWidth = 170;
    private const double NodeHeight = 60;
    private const double Padding = 10;
    private const double ArrowSize = 6;

    public GraphLayout? Layout { get; set; }
    public bool IsDarkMode { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Layout is null || Layout.Edges.Count == 0) return;

        var strokeColor = IsDarkMode
            ? Color.FromArgb("#7c7c9c")
            : Color.FromArgb("#9090b0");

        canvas.StrokeColor = strokeColor;
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;

        foreach (var edge in Layout.Edges)
        {
            var fromNode = Layout.Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
            var toNode = Layout.Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
            if (fromNode is null || toNode is null) continue;

            var fromCenterX = (float)(fromNode.X + Padding + NodeWidth / 2);
            var fromCenterY = (float)(fromNode.Y + Padding + NodeHeight / 2);
            var toCenterX = (float)(toNode.X + Padding + NodeWidth / 2);
            var toCenterY = (float)(toNode.Y + Padding + NodeHeight / 2);

            // Calculate edge endpoints at node borders
            var (startX, startY) = GetBorderPoint(fromCenterX, fromCenterY, toCenterX, toCenterY);
            var (endX, endY) = GetBorderPoint(toCenterX, toCenterY, fromCenterX, fromCenterY);

            canvas.DrawLine(startX, startY, endX, endY);

            // Draw arrowhead
            DrawArrow(canvas, startX, startY, endX, endY, strokeColor);
        }
    }

    private static (float x, float y) GetBorderPoint(float cx, float cy, float targetX, float targetY)
    {
        var dx = targetX - cx;
        var dy = targetY - cy;
        var halfW = (float)(NodeWidth / 2);
        var halfH = (float)(NodeHeight / 2);

        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return (cx, cy);

        // Use the ratio to determine which edge is hit
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
