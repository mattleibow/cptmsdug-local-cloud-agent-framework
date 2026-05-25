namespace Demo2.MauiAgent.Orchestrations;

public enum OrchestrationKind
{
    Sequential,
    Concurrent,
    Handoff,
    GroupChat
}

public record AgentDefinition(string Name, string SystemPrompt);
