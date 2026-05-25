using Microsoft.Maui.Controls.Shapes;

namespace Microsoft.Maui.AI.Agents.DevUI.Graph;

/// <summary>
/// A simple Border-based node view: rounded rect, label, status color stripe.
/// </summary>
public sealed class GraphNodeView : Border
{
    private readonly Label _label;
    private readonly BoxView _statusStripe;

    public GraphNodeView()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        Stroke = new SolidColorBrush(Color.FromArgb("#7A6FE8"));
        StrokeThickness = 1.5;
        BackgroundColor = Color.FromArgb("#1B1830");
        Padding = new Thickness(0);

        _statusStripe = new BoxView
        {
            Color = Color.FromArgb("#7A6FE8"),
            WidthRequest = 4,
            HorizontalOptions = LayoutOptions.Start,
        };

        _label = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            Padding = new Thickness(12, 4),
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            },
        };
        grid.Add(_statusStripe, 0, 0);
        grid.Add(_label, 1, 0);

        Content = grid;
    }

    public string Text
    {
        get => _label.Text ?? string.Empty;
        set => _label.Text = value;
    }

    public Color StatusColor
    {
        get => _statusStripe.Color;
        set => _statusStripe.Color = value;
    }
}
