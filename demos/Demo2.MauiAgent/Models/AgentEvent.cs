using CommunityToolkit.Mvvm.ComponentModel;

namespace Demo2.MauiAgent.Models;

public class AgentEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public partial class UIChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private int? _tokenCount;

    [ObservableProperty]
    private bool _isStreaming;
}

public class ToolCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public partial class WorkflowStep : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _status = "pending";

    [ObservableProperty]
    private DateTime? _startTime;

    [ObservableProperty]
    private DateTime? _endTime;
}
