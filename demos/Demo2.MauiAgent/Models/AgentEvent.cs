namespace Demo2.MauiAgent.Models;

public class AgentEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class UIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int? TokenCount { get; set; }
    public bool IsStreaming { get; set; }
}

public class ToolCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class WorkflowStep
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, running, completed
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
