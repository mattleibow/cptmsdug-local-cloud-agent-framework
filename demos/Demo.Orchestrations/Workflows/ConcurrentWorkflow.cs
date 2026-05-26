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

        // ── Specialist agents (each stays strictly in lane) ──────────────────

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

        // ── Post-processing agents (called from the aggregator) ──────────────

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

        // ── Outer workflow: BuildConcurrent + aggregator drives a clean post-pipeline ──
        //
        // Why not BuildSequential([specialists-wrapper, synthesizer, email-drafter])?
        // MAF's WorkflowHostAgent (created by AddAsAIAgent) emits the inner agents'
        // AgentResponseUpdate events as part of its own response — meaning the 3
        // specialists' tool_call/tool_result messages leak into the next stage's
        // conversation interleaved by time. OpenAI rejects interleaved tool_calls
        // with "An assistant message with 'tool_calls' must be followed by tool
        // messages...". So instead we keep one BuildConcurrent and let the aggregator
        // drive the post-processing pipeline manually with fresh, clean conversations
        // per step.

        var parallelAgents = new[]
        {
            "concurrent-travel-food",
            "concurrent-travel-culture",
            "concurrent-travel-logistics",
        };

        builder.AddWorkflow("concurrent-travel", (sp, key) =>
        {
            var labels = new[] { "FOOD EXPERT", "CULTURE EXPERT", "LOGISTICS EXPERT" };
            var synthesizer = sp.GetRequiredKeyedService<AIAgent>("travel-synthesizer");
            var emailer = sp.GetRequiredKeyedService<AIAgent>("travel-email-drafter");

            var workflow = AgentWorkflowBuilder.BuildConcurrent(
                workflowName: key,
                agents: parallelAgents
                    .Select(n => sp.GetRequiredKeyedService<AIAgent>(n))
                    .ToArray(),
                aggregator: results =>
                {
                    // 1. Collate the 3 specialists' final assistant outputs (no tool history)
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < results.Count && i < labels.Length; i++)
                    {
                        var last = results[i].LastOrDefault(m => m.Role == ChatRole.Assistant);
                        if (last is null) continue;
                        sb.AppendLine($"=== {labels[i]} ===");
                        sb.AppendLine(last.Text);
                        sb.AppendLine();
                    }
                    var collated = sb.ToString();

                    // 2. Synthesizer: fresh conversation, no leaked tool history
                    var synthResp = synthesizer.RunAsync(
                        [new ChatMessage(ChatRole.User, collated)])
                        .GetAwaiter().GetResult();
                    var tripPlan = synthResp.Messages
                        .LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text ?? "";

                    // 3. Email drafter: fresh conversation, only the synthesizer's plan
                    var emailResp = emailer.RunAsync(
                        [new ChatMessage(ChatRole.User, tripPlan)])
                        .GetAwaiter().GetResult();
                    var emailDraft = emailResp.Messages
                        .LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text ?? "";

                    // Return both the trip plan and the email draft as separate output messages
                    return
                    [
                        new ChatMessage(ChatRole.Assistant, tripPlan),
                        new ChatMessage(ChatRole.Assistant, $"---\n\n📧 **Email draft:**\n\n{emailDraft}")
                    ];
                });
            workflow.SetDescription(
                "Specialists fan out concurrently, a coordinator AI synthesises one trip " +
                "plan, then an email drafter turns it into a shareable summary.");
            return workflow;
        }).AddAsAIAgent();
    }
}
