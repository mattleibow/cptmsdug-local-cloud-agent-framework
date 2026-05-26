using Microsoft.Maui.Controls.Shapes;

namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// A simple Border-based node view: rounded rect with full background color for status,
/// label (2-line wrap), and description subtitle.
/// </summary>
public sealed class GraphNodeView : Border
{
    private readonly Label _label;
    private readonly Label _description;

    // Status-based colors
    private static readonly Color PendingBg = Color.FromArgb("#1B1830");
    private static readonly Color PendingBgLight = Color.FromArgb("#F3F1FA");
    private static readonly Color RunningBg = Color.FromArgb("#2D2200");
    private static readonly Color RunningBgLight = Color.FromArgb("#FFF3D0");
    private static readonly Color CompletedBg = Color.FromArgb("#0D2818");
    private static readonly Color CompletedBgLight = Color.FromArgb("#D9F2E0");
    private static readonly Color FailedBg = Color.FromArgb("#2D0A0A");
    private static readonly Color FailedBgLight = Color.FromArgb("#FDDEDE");
    private static readonly Color SkippedBg = Color.FromArgb("#1A1A1A");
    private static readonly Color SkippedBgLight = Color.FromArgb("#EEEEEE");

    private static readonly Color PendingStroke = Color.FromArgb("#7A6FE8");
    private static readonly Color RunningStroke = Color.FromArgb("#FFB300");
    private static readonly Color CompletedStroke = Color.FromArgb("#4CAF50");
    private static readonly Color FailedStroke = Color.FromArgb("#F44336");
    private static readonly Color SkippedStroke = Color.FromArgb("#666666");

    private bool _isDarkMode = true;

    public GraphNodeView()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        Stroke = new SolidColorBrush(PendingStroke);
        StrokeThickness = 2;
        BackgroundColor = PendingBg;
        Padding = new Thickness(12, 8);
        MinimumWidthRequest = 100;

        _label = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2,
        };

        _description = new Label
        {
            FontSize = 9,
            TextColor = Color.FromArgb("#AAAAAA"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            Margin = new Thickness(0, 2, 0, 0),
            IsVisible = false,
        };

        var textStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 0,
        };
        textStack.Add(_label);
        textStack.Add(_description);

        Content = textStack;
    }

    public string Text
    {
        get => _label.Text ?? string.Empty;
        set => _label.Text = value;
    }

    public string? DescriptionText
    {
        get => _description.Text;
        set
        {
            _description.Text = value;
            _description.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    public void SetStatus(string status, bool isDark)
    {
        _isDarkMode = isDark;
        var (bg, stroke, textColor) = status switch
        {
            "running" => (isDark ? RunningBg : RunningBgLight, RunningStroke, isDark ? Colors.White : Colors.Black),
            "completed" => (isDark ? CompletedBg : CompletedBgLight, CompletedStroke, isDark ? Colors.White : Colors.Black),
            "failed" => (isDark ? FailedBg : FailedBgLight, FailedStroke, isDark ? Colors.White : Colors.Black),
            "skipped" => (isDark ? SkippedBg : SkippedBgLight, SkippedStroke, isDark ? Color.FromArgb("#999999") : Color.FromArgb("#666666")),
            _ => (isDark ? PendingBg : PendingBgLight, PendingStroke, isDark ? Colors.White : Colors.Black),
        };

        BackgroundColor = bg;
        Stroke = new SolidColorBrush(stroke);
        _label.TextColor = textColor;
        _description.TextColor = isDark ? Color.FromArgb("#AAAAAA") : Color.FromArgb("#666666");
    }

    // Keep backward compat — defaults to dark mode
    public Color StatusColor
    {
        set => Stroke = new SolidColorBrush(value);
    }
}
