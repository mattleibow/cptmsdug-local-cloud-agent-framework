using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.MauiAgentApp.Orchestrations;

public static class StandaloneAgents
{
    public static void AddStandaloneAgents(this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: "storyteller",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
                name: key,
                description: "Creative storyteller that writes imaginative short stories with vivid language.",
                instructions: """
                    You are a creative storyteller. Write imaginative
                    short stories (300 words or less) about any topic
                    the user provides. Use vivid language and
                    surprising twists.
                    """).WithTelemetry());

        builder.AddAIAgent(
            name: "code-mentor",
            (sp, key) => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                name: key,
                description: "Coding mentor that explains concepts clearly and helps debug code.",
                instructions: """
                    You are a friendly coding mentor. Explain
                    programming concepts clearly with examples.
                    Help debug code and suggest best practices.
                    Keep explanations concise and practical.
                    """).WithTelemetry());
    }
}

