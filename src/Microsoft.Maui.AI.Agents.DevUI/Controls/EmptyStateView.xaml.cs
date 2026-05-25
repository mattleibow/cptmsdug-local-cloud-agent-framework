using System.Windows.Input;

namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), "Select an agent or workflow");

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(EmptyStateView));

    public static readonly BindableProperty HowItWorksProperty =
        BindableProperty.Create(nameof(HowItWorks), typeof(string), typeof(EmptyStateView));

    public static readonly BindableProperty DemoPromptProperty =
        BindableProperty.Create(nameof(DemoPrompt), typeof(string), typeof(EmptyStateView));

    public static readonly BindableProperty RunDemoCommandProperty =
        BindableProperty.Create(nameof(RunDemoCommand), typeof(ICommand), typeof(EmptyStateView));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? HowItWorks
    {
        get => (string?)GetValue(HowItWorksProperty);
        set => SetValue(HowItWorksProperty, value);
    }

    public string? DemoPrompt
    {
        get => (string?)GetValue(DemoPromptProperty);
        set => SetValue(DemoPromptProperty, value);
    }

    public ICommand? RunDemoCommand
    {
        get => (ICommand?)GetValue(RunDemoCommandProperty);
        set => SetValue(RunDemoCommandProperty, value);
    }

    public EmptyStateView()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
    }

    private void OnDemoChipTapped(object? sender, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(DemoPrompt) && RunDemoCommand?.CanExecute(DemoPrompt) == true)
            RunDemoCommand.Execute(DemoPrompt);
    }
}
