using System.Collections;
using Microsoft.Maui.AI.Agents.DevUI;

namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class DebugPanelView : ContentView
{
    public static readonly BindableProperty EventsProperty =
        BindableProperty.Create(nameof(Events), typeof(IEnumerable), typeof(DebugPanelView));

    public static readonly BindableProperty TracesProperty =
        BindableProperty.Create(nameof(Traces), typeof(IEnumerable), typeof(DebugPanelView));

    public static readonly BindableProperty ToolCallsProperty =
        BindableProperty.Create(nameof(ToolCalls), typeof(IEnumerable), typeof(DebugPanelView));

    public IEnumerable? Events
    {
        get => (IEnumerable?)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public IEnumerable? Traces
    {
        get => (IEnumerable?)GetValue(TracesProperty);
        set => SetValue(TracesProperty, value);
    }

    public IEnumerable? ToolCalls
    {
        get => (IEnumerable?)GetValue(ToolCallsProperty);
        set => SetValue(ToolCallsProperty, value);
    }

    public DebugPanelView()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
    }

    private void OnEventsTabClicked(object? sender, EventArgs e) => SwitchTab("events");
    private void OnTracesTabClicked(object? sender, EventArgs e) => SwitchTab("traces");
    private void OnToolsTabClicked(object? sender, EventArgs e) => SwitchTab("tools");

    private void SwitchTab(string tab)
    {
        EventsList.IsVisible = tab == "events";
        TracesList.IsVisible = tab == "traces";
        ToolsList.IsVisible = tab == "tools";

        SetTabStyle(EventsTabBtn, tab == "events");
        SetTabStyle(TracesTabBtn, tab == "traces");
        SetTabStyle(ToolsTabBtn, tab == "tools");
    }

    private void SetTabStyle(Button btn, bool active)
    {
        btn.TextColor = active ? Color.FromArgb("#643FB2") : Colors.Gray;
        btn.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
    }

    private void OnToolCallTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is DevUIToolCall tc)
            tc.IsExpanded = !tc.IsExpanded;
    }
}
