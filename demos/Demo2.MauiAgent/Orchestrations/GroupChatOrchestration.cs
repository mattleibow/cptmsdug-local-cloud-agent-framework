using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.Orchestrations;

/// <summary>
/// Group Chat orchestration: Agents collaborate in a shared conversation.
/// Theme: Product Design Review (Designer, Engineer, PM discuss a feature)
/// </summary>
public class GroupChatOrchestration
{
    public string Name => "Group Chat: Design Review";
    public string Description => "Designer, Engineer, and PM collaborate on a feature design";
    public OrchestrationKind Kind => OrchestrationKind.GroupChat;

    public int MaxRounds => 3;

    public IReadOnlyList<AgentDefinition> Agents { get; } =
    [
        new("designer", "You are a UX designer in a product design review. Evaluate ideas from a usability, aesthetics, and user experience perspective. Challenge engineering constraints when they hurt UX. Keep contributions to 100 words. Address other participants by name."),
        new("engineer", "You are a software engineer in a product design review. Evaluate ideas from a technical feasibility, performance, and maintainability perspective. Suggest alternatives when designs are too complex. Keep contributions to 100 words. Address other participants by name."),
        new("product-manager", "You are a product manager in a product design review. Evaluate ideas from a business value, user impact, and timeline perspective. Mediate between design and engineering. Summarize decisions. Keep contributions to 100 words. Address other participants by name.")
    ];
}

public static class GroupChatOrchestrationExtensions
{
    public static IServiceCollection AddGroupChatWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<GroupChatOrchestration>();
        return services;
    }
}
