using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations;

public static class StandaloneAgents
{
    public static void AddStandaloneAgents(
        this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: "storyteller",
            instructions: """
                You are a creative storyteller. Write imaginative
                short stories (300 words or less) about any topic
                the user provides. Use vivid language and
                surprising twists.
                """,
            description:
                "Creative storyteller that writes imaginative " +
                "short stories with vivid language.",
            chatClientServiceKey: null);

        builder.AddAIAgent(
            name: "code-mentor",
            instructions: """
                You are a friendly coding mentor. Explain
                programming concepts clearly with examples.
                Help debug code and suggest best practices.
                Keep explanations concise and practical.
                """,
            description:
                "Coding mentor that explains concepts clearly " +
                "and helps debug code.",
            chatClientServiceKey: null);
    }
}
