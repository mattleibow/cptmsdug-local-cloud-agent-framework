namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class WorkflowNodeView : ContentView
{
    public static readonly BindableProperty NodeProperty =
        BindableProperty.Create(nameof(Node), typeof(WorkflowNode), typeof(WorkflowNodeView),
            propertyChanged: OnNodeChanged);

    public static readonly BindableProperty StatusProperty =
        BindableProperty.Create(nameof(Status), typeof(string), typeof(WorkflowNodeView), "pending");

    public static readonly BindableProperty DisplayNameProperty =
        BindableProperty.Create(nameof(DisplayName), typeof(string), typeof(WorkflowNodeView), string.Empty);

    public static readonly BindableProperty StatusIconProperty =
        BindableProperty.Create(nameof(StatusIcon), typeof(string), typeof(WorkflowNodeView), "\u25CB");

    public static readonly BindableProperty StatusLabelProperty =
        BindableProperty.Create(nameof(StatusLabel), typeof(string), typeof(WorkflowNodeView), "Pending");

    public WorkflowNode? Node
    {
        get => (WorkflowNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public string StatusIcon
    {
        get => (string)GetValue(StatusIconProperty);
        set => SetValue(StatusIconProperty, value);
    }

    public string StatusLabel
    {
        get => (string)GetValue(StatusLabelProperty);
        set => SetValue(StatusLabelProperty, value);
    }

    public WorkflowNodeView()
    {
        InitializeComponent();
    }

    private static void OnNodeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not WorkflowNodeView view) return;

        if (oldValue is WorkflowNode oldNode)
            oldNode.PropertyChanged -= view.OnNodePropertyChanged;

        if (newValue is WorkflowNode node)
        {
            node.PropertyChanged += view.OnNodePropertyChanged;
            view.UpdateFromNode(node);
        }
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is WorkflowNode node)
            MainThread.BeginInvokeOnMainThread(() => UpdateFromNode(node));
    }

    private void UpdateFromNode(WorkflowNode node)
    {
        DisplayName = GetShortName(node.Name);
        Status = node.Status;
        StatusIcon = node.StatusIcon;
        StatusLabel = node.StatusLabel;
    }

    private static string GetShortName(string name)
    {
        if (name.Length <= 20 || !name.Contains('-'))
            return name;

        var parts = name.Split('-');
        return parts.Length > 2
            ? string.Join("-", parts.Skip(parts.Length - 2))
            : parts[^1];
    }
}
