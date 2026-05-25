using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class ChatPanelView : ContentView
{
    public static readonly BindableProperty MessagesProperty =
        BindableProperty.Create(nameof(Messages), typeof(ObservableCollection<DevUIChatMessage>), typeof(ChatPanelView));

    public static readonly BindableProperty IsSendingProperty =
        BindableProperty.Create(nameof(IsSending), typeof(bool), typeof(ChatPanelView));

    public static readonly BindableProperty SendCommandProperty =
        BindableProperty.Create(nameof(SendCommand), typeof(ICommand), typeof(ChatPanelView));

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ChatPanelView), "Type a message...");

    public static readonly BindableProperty EmptyTitleProperty =
        BindableProperty.Create(nameof(EmptyTitle), typeof(string), typeof(ChatPanelView), "Select an agent or workflow");

    public static readonly BindableProperty EmptyDescriptionProperty =
        BindableProperty.Create(nameof(EmptyDescription), typeof(string), typeof(ChatPanelView));

    public ObservableCollection<DevUIChatMessage>? Messages
    {
        get => (ObservableCollection<DevUIChatMessage>?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public bool IsSending
    {
        get => (bool)GetValue(IsSendingProperty);
        set => SetValue(IsSendingProperty, value);
    }

    public ICommand? SendCommand
    {
        get => (ICommand?)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string EmptyTitle
    {
        get => (string)GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string? EmptyDescription
    {
        get => (string?)GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    public ChatPanelView()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
    }
}
