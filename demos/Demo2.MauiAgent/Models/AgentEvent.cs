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

    [ObservableProperty]
    private string? _agentLabel;
}

public class ToolCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class TraceSpan
{
    public string Name { get; set; } = string.Empty;
    public string OperationKind { get; set; } = "LLM";
    public int Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public Color OperationColor => OperationKind switch
    {
        "LLM" => Color.FromArgb("#8B5CF6"),
        "Tool" => Color.FromArgb("#3B82F6"),
        "Agent" => Color.FromArgb("#10B981"),
        _ => Color.FromArgb("#6B7280")
    };
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

    [ObservableProperty]
    private bool _isFirst;

    public string StatusIcon => Status switch
    {
        "running" => "▶",
        "completed" => "✓",
        "failed" => "✗",
        "skipped" => "⊘",
        _ => "○"
    };

    public string StatusLabel => Status switch
    {
        "running" => "Running...",
        "completed" => EndTime.HasValue && StartTime.HasValue
            ? $"Done ({(EndTime.Value - StartTime.Value).TotalSeconds:F1}s)"
            : "Done",
        "failed" => "Failed",
        "skipped" => "Skipped",
        _ => "Pending"
    };

    public bool IsRunning => Status == "running";

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsRunning));
    }

    partial void OnEndTimeChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(StatusLabel));
    }
}
