using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Demo.Orchestrations.Tools;

namespace Demo.Orchestrations;

public static class ConcurrentWorkflow
{
    public static void AddConcurrentWorkflow(this IHostApplicationBuilder builder)
    {
        var travelTools = TravelToolContext.Default.Tools;

        // ── Specialist agents (fan out — each stays strictly in lane) ────────

        builder.AddAIAgent("concurrent-travel-food", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Culinary expert recommending restaurants and local dishes.",
            instructions: """
                You are a culinary travel expert. The user will tell you a destination. Your job
                is ONLY food recommendations — NOT a day-by-day itinerary.

                1. Call search_restaurants for the destination
                2. Recommend 4-6 must-try restaurants and 2-3 iconic local dishes
                3. Include price ranges and booking tips

                DO NOT plan a daily schedule. DO NOT mention transport, accommodation, or
                attractions. Stay strictly in your lane. Keep to 120 words.
                """,
            tools: [.. travelTools.Where(t => t.Name == "search_restaurants")]
        ));

        builder.AddAIAgent("concurrent-travel-culture", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Cultural expert recommending museums, sites, and local experiences.",
            instructions: """
                You are a cultural travel expert. The user will tell you a destination. Your job
                is ONLY cultural attractions — NOT a day-by-day itinerary.

                Recommend 5-7 must-see museums, historical sites, neighbourhoods, and cultural
                experiences for the destination. Include opening hours and one local insider tip
                per item.

                DO NOT plan a daily schedule. DO NOT mention restaurants, transport, or hotels.
                Stay strictly in your lane. Keep to 120 words.
                """,
            tools: []
        ));

        builder.AddAIAgent("concurrent-travel-logistics", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Logistics expert for transport, accommodation, and routing.",
            instructions: """
                You are a travel logistics expert. The user will tell you a destination and trip
                duration. Your job is ONLY transport and accommodation — NOT a day-by-day
                itinerary of activities.

                1. Call check_accommodation for the destination
                2. Call check_transport for 2-3 common routes
                3. Recommend the best neighbourhoods to stay in
                4. Give total budget estimate (transport + accommodation only)

                DO NOT plan daily activities. DO NOT mention restaurants or attractions. Stay
                strictly in your lane. Keep to 120 words.
                """,
            tools: [.. travelTools.Where(t =>
                t.Name is "check_transport" or "check_accommodation")]
        ));

        // ── Fan-out workflow: specialists run in parallel, aggregator just collates ──

        var parallelAgents = new[]
        {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics",
        };

        builder.AddWorkflow("concurrent-travel-specialists", (sp, key) =>
        {
            var labels = new[] { "FOOD EXPERT", "CULTURE EXPERT", "LOGISTICS EXPERT" };

            return AgentWorkflowBuilder.BuildConcurrent(
                workflowName: key,
                agents: parallelAgents
                    .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                    .ToArray(),
                aggregator: results =>
                {
                    // No AI here — just collate labeled output, hand off to next agent
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < results.Count && i < labels.Length; i++)
                    {
                        var last = results[i].LastOrDefault(m => m.Role == ChatRole.Assistant);
                        if (last is null) continue;
                        sb.AppendLine($"=== {labels[i]} ===");
                        sb.AppendLine(last.Text);
                        sb.AppendLine();
                    }
                    // ChatRole.User so the next agent in the pipeline treats it as input
                    return [new ChatMessage(ChatRole.User, sb.ToString())];
                });
        }).AddAsAIAgent(); // ← exposes the inner workflow as a step

        // ── Fan-in: synthesizer is a real registered agent ───────────────────

        builder.AddAIAgent("travel-synthesizer", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Weaves specialist inputs into one cohesive trip plan.",
            instructions: """
                You are a senior travel coordinator. Three specialists (food, culture, logistics)
                have each provided independent input. Weave them into ONE cohesive trip plan.

                Required output structure (Markdown):
                  # Trip Plan: <destination>
                  ## Overview
                  (2-3 sentence summary of the trip vibe)
                  ## Day-by-Day Itinerary
                  (suggested days that interleave attractions, meals, and transport — use the
                  specialists' inputs as source material)
                  ## Where to Stay
                  ## Estimated Budget
                  ## Quick Tips

                Synthesise and de-duplicate — do NOT just concatenate. If a specialist mentioned
                something irrelevant, drop it. Aim for 350-500 words total.
                """,
            tools: []
        ));

        // ── Optional follow-on agents (sequential after the synthesizer) ─────

        builder.AddAIAgent("travel-email-drafter", (sp, key) => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            name: key,
            description: "Turns the trip plan into a shareable email draft.",
            instructions: """
                You are an email drafter. Take the trip plan you receive and convert it into a
                friendly, concise email (under 200 words) that the user could send to a travel
                companion. Use this structure:

                Subject: <subject line>

                Hi <friend>,

                <2-3 short paragraphs summarising the highlights — attractions, food, where to
                stay, rough budget. Keep it warm and exciting, not bureaucratic.>

                Cheers,
                <name>
                """,
            tools: []
        ));

        // ── Outer sequential workflow ties it all together ───────────────────

        builder.AddWorkflow("concurrent-travel", (sp, key) =>
        {
            var workflow = AgentWorkflowBuilder.BuildSequential(
                workflowName: key,
                agents:
                [
                    sp.GetRequiredKeyedService<AIAgent>("concurrent-travel-specialists"),
                    sp.GetRequiredKeyedService<AIAgent>("travel-synthesizer"),
                    sp.GetRequiredKeyedService<AIAgent>("travel-email-drafter"),
                ]);

            workflow.SetDescription(
                "Specialists fan out concurrently, a coordinator synthesises one trip plan, " +
                "then an email drafter turns it into a shareable summary.");
            return workflow;
        }).AddAsAIAgent();
    }
}
