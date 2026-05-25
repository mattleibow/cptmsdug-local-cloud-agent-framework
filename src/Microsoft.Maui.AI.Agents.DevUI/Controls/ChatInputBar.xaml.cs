using System.Windows.Input;

namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class ChatInputBar : ContentView
{
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ChatInputBar), "Type a message...");

    public static readonly BindableProperty IsSendingProperty =
        BindableProperty.Create(nameof(IsSending), typeof(bool), typeof(ChatInputBar), false);

    public static readonly BindableProperty SendCommandProperty =
        BindableProperty.Create(nameof(SendCommand), typeof(ICommand), typeof(ChatInputBar));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
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

    public ChatInputBar()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
    }

    private void OnSendClicked(object? sender, EventArgs e) => ExecuteSend();
    private void OnEntryCompleted(object? sender, EventArgs e) => ExecuteSend();

    private void ExecuteSend()
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (SendCommand?.CanExecute(text) == true)
        {
            SendCommand.Execute(text);
            MessageEntry.Text = string.Empty;
        }
    }
}
