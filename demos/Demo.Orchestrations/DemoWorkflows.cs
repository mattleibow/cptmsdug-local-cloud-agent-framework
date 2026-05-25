using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Demo.Orchestrations;

/// <summary>
/// Central registry of all demo orchestrations and their agent definitions.
/// Both Demo1 (web/MAF) and Demo2 (native/MAUI) consume these definitions.
/// </summary>
public static class DemoWorkflows
{
    /// <summary>All standalone agents available for direct chat.</summary>
    public static IReadOnlyList<AgentDefinition> StandaloneAgents { get; } =
    [
        new("writer", "You write short stories (300 words or less) about the specified topic."),
        new("editor", "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing.")
    ];

    /// <summary>All workflow definitions.</summary>
    public static IReadOnlyList<WorkflowDefinition> Workflows =>
    [
        Sequential,
        Concurrent,
        Handoff,
        GroupChat
    ];

    /// <summary>
    /// Sequential: Story Pipeline (Writer -> Editor -> Publisher)
    /// </summary>
    public static WorkflowDefinition Sequential { get; } = new(
        Id: "sequential-story",
        Name: "Story Pipeline",
        Description: "Writer drafts, Editor refines, Publisher formats",
        Kind: OrchestrationKind.Sequential,
        DemoPrompt: "Write a short story about a robot learning to paint",
        Agents:
        [
            new("writer", "You write short stories (300 words or less) about the specified topic."),
            new("editor", "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing."),
            new("publisher", "You take the final story and format it with a catchy headline and a brief teaser.")
        ],
        Tools: [AIFunctionFactory.Create(FormatStory)]);

    /// <summary>
    /// Concurrent: Research Briefing (parallel analysts + synthesizer)
    /// </summary>
    public static WorkflowDefinition Concurrent { get; } = new(
        Id: "concurrent-research",
        Name: "Research Briefing",
        Description: "Multiple analysts research in parallel, then merge findings",
        Kind: OrchestrationKind.Concurrent,
        DemoPrompt: "Analyze the impact of quantum computing on cybersecurity",
        Agents:
        [
            new("technical-analyst", "You are a technical analyst. Analyze the technical aspects, feasibility, and implementation details of the given topic. Keep analysis to 150 words."),
            new("market-analyst", "You are a market analyst. Analyze the market opportunity, competition, and business potential of the given topic. Keep analysis to 150 words."),
            new("risk-analyst", "You are a risk analyst. Identify potential risks, challenges, and mitigation strategies for the given topic. Keep analysis to 150 words."),
            new("synthesizer", "You are a synthesis expert. Take multiple analysis reports and combine them into a coherent executive briefing of 200 words or less.")
        ]);

    /// <summary>
    /// Handoff: Customer Support (triage routes to specialist)
    /// </summary>
    public static WorkflowDefinition Handoff { get; } = new(
        Id: "handoff-support",
        Name: "Customer Support",
        Description: "Triage agent routes to the right specialist based on issue",
        Kind: OrchestrationKind.Handoff,
        DemoPrompt: "I need to return a defective laptop that keeps crashing",
        Agents:
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
        ]);

    /// <summary>
    /// GroupChat: Design Review (designer, engineer, PM collaborate)
    /// </summary>
    public static WorkflowDefinition GroupChat { get; } = new(
        Id: "groupchat-design",
        Name: "Design Review",
        Description: "Designer, Engineer, and PM collaborate on a feature",
        Kind: OrchestrationKind.GroupChat,
        DemoPrompt: "Design a mobile banking app for Gen Z users",
        Agents:
        [
            new("designer", "You are a UX designer in a product design review. Evaluate ideas from a usability, aesthetics, and user experience perspective. Challenge engineering constraints when they hurt UX. Keep contributions to 100 words. Address other participants by name."),
            new("engineer", "You are a software engineer in a product design review. Evaluate ideas from a technical feasibility, performance, and maintainability perspective. Suggest alternatives when designs are too complex. Keep contributions to 100 words. Address other participants by name."),
            new("product-manager", "You are a product manager in a product design review. Evaluate ideas from a business value, user impact, and timeline perspective. Mediate between design and engineering. Summarize decisions. Keep contributions to 100 words. Address other participants by name.")
        ]);

    [Description("Formats the story for publication, revealing its title.")]
    private static string FormatStory(string title, string story) => $"""
        # {title}

        {story}
        """;
}
