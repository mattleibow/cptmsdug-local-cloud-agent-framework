using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Hosting;

namespace Demo.Orchestrations;

public static class StandaloneAgents
{
    public static void AddStandaloneAgents(this IHostApplicationBuilder builder)
    {
        builder.AddAIAgent("storyteller", """
            You are a creative storyteller. Write imaginative short stories (300 words or less)
            about any topic the user provides. Use vivid language and surprising twists.
            """);
        builder.AddAIAgent("code-mentor", """
            You are a friendly coding mentor. Explain programming concepts clearly with examples.
            Help debug code and suggest best practices. Keep explanations concise and practical.
            """);
    }
}
