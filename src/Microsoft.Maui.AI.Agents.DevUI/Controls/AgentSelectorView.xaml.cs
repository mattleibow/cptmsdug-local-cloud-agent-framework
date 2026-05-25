namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class AgentSelectorView : ContentView
{
    public static readonly BindableProperty AgentsProperty =
        BindableProperty.Create(nameof(Agents), typeof(IReadOnlyList<AgentInfo>), typeof(AgentSelectorView),
            propertyChanged: OnEntitiesChanged);

    public static readonly BindableProperty WorkflowsProperty =
        BindableProperty.Create(nameof(Workflows), typeof(IReadOnlyList<WorkflowInfo>), typeof(AgentSelectorView),
            propertyChanged: OnEntitiesChanged);

    public static readonly BindableProperty SelectedEntityProperty =
        BindableProperty.Create(nameof(SelectedEntity), typeof(object), typeof(AgentSelectorView),
            defaultBindingMode: BindingMode.TwoWay);

    public IReadOnlyList<AgentInfo>? Agents
    {
        get => (IReadOnlyList<AgentInfo>?)GetValue(AgentsProperty);
        set => SetValue(AgentsProperty, value);
    }

    public IReadOnlyList<WorkflowInfo>? Workflows
    {
        get => (IReadOnlyList<WorkflowInfo>?)GetValue(WorkflowsProperty);
        set => SetValue(WorkflowsProperty, value);
    }

    public object? SelectedEntity
    {
        get => GetValue(SelectedEntityProperty);
        set => SetValue(SelectedEntityProperty, value);
    }

    private Border? _selectedBorder;

    public AgentSelectorView()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
    }

    private static void OnEntitiesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AgentSelectorView view)
            view.RebuildList();
    }

    private void RebuildList()
    {
        SelectorStack.Children.Clear();

        if (Agents is { Count: > 0 })
        {
            SelectorStack.Add(new Label
            {
                Text = "AGENTS",
                Style = (Style)Resources["DevUI.SectionHeader"]
            });

            foreach (var agent in Agents)
            {
                SelectorStack.Add(CreateEntityButton(agent.Name, agent.Description, () =>
                {
                    SelectedEntity = agent;
                }));
            }
        }

        if (Workflows is { Count: > 0 })
        {
            SelectorStack.Add(new Label
            {
                Text = "WORKFLOWS",
                Style = (Style)Resources["DevUI.SectionHeader"]
            });

            foreach (var workflow in Workflows)
            {
                var kindIcon = workflow.Kind switch
                {
                    OrchestrationKind.Sequential => "\u2192",
                    OrchestrationKind.Concurrent => "\u2261",
                    OrchestrationKind.Handoff => "\u21C4",
                    OrchestrationKind.GroupChat => "\u25CB",
                    _ => "\u2022"
                };
                SelectorStack.Add(CreateEntityButton(
                    $"{kindIcon} {workflow.Name}",
                    workflow.Description,
                    () => { SelectedEntity = workflow; }));
            }
        }
    }

    private View CreateEntityButton(string name, string? description, Action onTap)
    {
        var border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = new SolidColorBrush(Colors.Transparent),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(10, 8),
            Margin = new Thickness(4, 1)
        };

        var stack = new VerticalStackLayout { Spacing = 2 };

        var nameLabel = new Label
        {
            Text = name,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
        };
        nameLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1a1a2e"), Color.FromArgb("#f0f0f0"));
        stack.Add(nameLabel);

        if (!string.IsNullOrEmpty(description))
        {
            stack.Add(new Label
            {
                Text = description,
                FontSize = 10,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            });
        }

        border.Content = stack;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            if (_selectedBorder is not null)
                _selectedBorder.BackgroundColor = Colors.Transparent;

            border.BackgroundColor = Color.FromArgb("#643FB220");
            _selectedBorder = border;
            onTap();
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }
}
