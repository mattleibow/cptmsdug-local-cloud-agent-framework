using System.ComponentModel;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// A chat message displayed in the DevUI conversation area.
/// </summary>
public sealed class DevUIChatMessage : INotifyPropertyChanged
{
    private string _content = string.Empty;
    private bool _isStreaming;
    private int? _tokenCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Message role: "user" or "assistant".</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>The message text content.</summary>
    public string Content
    {
        get => _content;
        set { _content = value; PropertyChanged?.Invoke(this, new(nameof(Content))); }
    }

    /// <summary>When the message was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>Whether this message is still being streamed.</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set { _isStreaming = value; PropertyChanged?.Invoke(this, new(nameof(IsStreaming))); }
    }

    /// <summary>Estimated token count for the message.</summary>
    public int? TokenCount
    {
        get => _tokenCount;
        set { _tokenCount = value; PropertyChanged?.Invoke(this, new(nameof(TokenCount))); }
    }

    /// <summary>Agent name label for workflow messages.</summary>
    public string? AgentLabel { get; init; }

    /// <summary>Whether this is a user message.</summary>
    public bool IsUser => Role == "user";

    /// <summary>Initials for the avatar circle (1-2 chars).</summary>
    public string AvatarInitials => IsUser ? "U" : GetInitials(AgentLabel ?? "AI");

    /// <summary>Background color for the avatar circle, deterministic per agent name.</summary>
    public Color AvatarColor => IsUser
        ? Color.FromArgb("#643FB2")
        : GetColorForName(AgentLabel ?? "AI");

    private static string GetInitials(string name)
    {
        var parts = name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
        return name.Length >= 2
            ? $"{char.ToUpper(name[0])}{char.ToUpper(name[1])}"
            : name[..1].ToUpper();
    }

    private static readonly string[] s_avatarColors =
    [
        "#2563EB", "#7C3AED", "#DC2626", "#059669",
        "#D97706", "#0891B2", "#BE185D", "#4F46E5",
        "#15803D", "#9333EA", "#B91C1C", "#0D9488"
    ];

    private static Color GetColorForName(string name)
    {
        var hash = name.GetHashCode(StringComparison.OrdinalIgnoreCase);
        var idx = Math.Abs(hash) % s_avatarColors.Length;
        return Color.FromArgb(s_avatarColors[idx]);
    }
}

/// <summary>
/// An event in the DevUI event log.
/// </summary>
public sealed class DevUIEvent
{
    /// <summary>When the event occurred.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>Event type identifier.</summary>
    public required string Type { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Optional extra details.</summary>
    public string? Details { get; init; }
}

/// <summary>
/// A trace span representing a timed operation.
/// </summary>
public sealed class DevUITraceSpan
{
    /// <summary>Operation name.</summary>
    public required string Name { get; init; }

    /// <summary>Kind of operation: LLM, Tool, Agent.</summary>
    public required string OperationKind { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public int Duration { get; init; }

    /// <summary>When the span started.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>Color for the operation badge.</summary>
    public Color OperationColor => OperationKind switch
    {
        "LLM" => Color.FromArgb("#8B5CF6"),
        "Tool" => Color.FromArgb("#3B82F6"),
        "Agent" => Color.FromArgb("#10B981"),
        _ => Color.FromArgb("#6B7280")
    };
}

/// <summary>
/// A tool/function call captured during agent execution.
/// </summary>
public sealed class DevUIToolCall : INotifyPropertyChanged
{
    private string? _result;

    /// <summary>Function name.</summary>
    public required string Name { get; init; }

    /// <summary>Serialized arguments.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>The call ID used to match results to calls.</summary>
    public string? CallId { get; init; }

    /// <summary>Optional result populated when the tool returns.</summary>
    public string? Result
    {
        get => _result;
        set { _result = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Result))); }
    }

    /// <summary>When the tool was called.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Represents a node in the workflow execution graph.
/// </summary>
public sealed class WorkflowNode : INotifyPropertyChanged
{
    private string _status = "pending";
    private DateTime? _startTime;
    private DateTime? _endTime;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Unique ID matching the executor.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Execution status: pending, running, completed, failed, skipped.</summary>
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new(nameof(Status)));
            PropertyChanged?.Invoke(this, new(nameof(StatusIcon)));
            PropertyChanged?.Invoke(this, new(nameof(StatusLabel)));
            PropertyChanged?.Invoke(this, new(nameof(IsRunning)));
        }
    }

    /// <summary>When execution started.</summary>
    public DateTime? StartTime
    {
        get => _startTime;
        set { _startTime = value; PropertyChanged?.Invoke(this, new(nameof(StartTime))); }
    }

    /// <summary>When execution completed.</summary>
    public DateTime? EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            PropertyChanged?.Invoke(this, new(nameof(EndTime)));
            PropertyChanged?.Invoke(this, new(nameof(StatusLabel)));
        }
    }

    /// <summary>Whether this node is currently executing.</summary>
    public bool IsRunning => Status == "running";

    /// <summary>Computed position X for graph layout.</summary>
    public double X { get; set; }

    /// <summary>Computed position Y for graph layout.</summary>
    public double Y { get; set; }

    /// <summary>Unicode status indicator glyph.</summary>
    public string StatusIcon => Status switch
    {
        "running" => "\u25B6",   // ▶
        "completed" => "\u2713", // ✓
        "failed" => "\u2717",    // ✗
        "skipped" => "\u2298",   // ⊘
        _ => "\u25CB"            // ○
    };

    /// <summary>Human-readable status text.</summary>
    public string StatusLabel => Status switch
    {
        "running" => "Running\u2026",
        "completed" => EndTime.HasValue && StartTime.HasValue
            ? $"Done ({(EndTime.Value - StartTime.Value).TotalSeconds:F1}s)"
            : "Done",
        "failed" => "Failed",
        "skipped" => "Skipped",
        _ => "Pending"
    };
}
