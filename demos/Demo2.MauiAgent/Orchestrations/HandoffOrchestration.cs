using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.Orchestrations;

/// <summary>
/// Handoff orchestration: Agents transfer control to each other based on context.
/// Theme: Customer Support (Triage → Specialist handoff based on issue type)
/// </summary>
public class HandoffOrchestration
{
    public string Name => "Handoff: Customer Support";
    public string Description => "Triage agent routes to the right specialist based on issue";
    public OrchestrationKind Kind => OrchestrationKind.Handoff;

    public IReadOnlyList<AgentDefinition> Agents { get; } =
    [
        new("triage", """
            You are a customer support triage agent. Analyze the customer's issue and determine which specialist should handle it.
            Respond with EXACTLY one of these routing decisions:
            - ROUTE:billing - for payment, subscription, or pricing issues
            - ROUTE:technical - for bugs, errors, or technical problems
            - ROUTE:account - for login, password, or account access issues
            After the routing tag, briefly explain why you're routing there.
            """),
        new("billing", "You are a billing specialist. Help customers with payment issues, subscription changes, refunds, and pricing questions. Be empathetic and solution-oriented. Keep responses under 200 words."),
        new("technical", "You are a technical support specialist. Help customers debug issues, explain error messages, and provide step-by-step solutions. Be precise and technical. Keep responses under 200 words."),
        new("account", "You are an account specialist. Help customers with login issues, password resets, account recovery, and access problems. Be patient and clear. Keep responses under 200 words.")
    ];
}

public static class HandoffOrchestrationExtensions
{
    public static IServiceCollection AddHandoffWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<HandoffOrchestration>();
        return services;
    }
}
