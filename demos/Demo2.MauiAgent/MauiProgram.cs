using Demo2.MauiAgent.Orchestrations;
using Demo2.MauiAgent.Services;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Agents.DevUI;
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Demo2.MauiAgent;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		// Load embedded user secrets
		AIChatService.AddUserSecrets(builder.Configuration);

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.AddMauiDevFlowAgent();
#endif

		// Register services
		builder.Services.AddSingleton<AIChatService>();
		builder.Services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<AIChatService>().ChatClient);

		// Register orchestrations
		builder.Services
			.AddSequentialWorkflow()
			.AddConcurrentWorkflow()
			.AddHandoffWorkflow()
			.AddGroupChatWorkflow();

		// Register DevUI with agent/workflow discovery
		builder.Services.AddMauiAgentDevUI();
		RegisterDevUIEntities(builder.Services);

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void RegisterDevUIEntities(IServiceCollection services)
	{
		// Register agents
		services.AddDevUIAgent(new AgentInfo
		{
			Id = "writer",
			Name = "writer",
			Description = "Writes short stories about any topic",
			Instructions = "You write short stories (300 words or less) about the specified topic."
		});
		services.AddDevUIAgent(new AgentInfo
		{
			Id = "editor",
			Name = "editor",
			Description = "Edits stories for grammar, style, and formatting",
			Instructions = "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words."
		});

		// Register workflows
		services.AddDevUIWorkflow(new WorkflowInfo
		{
			Id = "sequential-story",
			Name = "Story Pipeline",
			Description = "Writer drafts, Editor refines, Publisher formats",
			Kind = Microsoft.Maui.AI.Agents.DevUI.OrchestrationKind.Sequential,
			DemoPrompt = "Write a short story about a robot learning to paint",
			Executors =
			[
				new() { Id = "writer", Name = "writer", SystemPrompt = "You write short stories (300 words or less) about the specified topic." },
				new() { Id = "editor", Name = "editor", SystemPrompt = "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing." },
				new() { Id = "publisher", Name = "publisher", SystemPrompt = "You take the final story and format it with a catchy headline and a brief teaser." }
			]
		});

		services.AddDevUIWorkflow(new WorkflowInfo
		{
			Id = "concurrent-research",
			Name = "Research Briefing",
			Description = "Multiple analysts research in parallel, then merge findings",
			Kind = Microsoft.Maui.AI.Agents.DevUI.OrchestrationKind.Concurrent,
			DemoPrompt = "Analyze the impact of quantum computing on cybersecurity",
			Executors =
			[
				new() { Id = "technical-analyst", Name = "technical-analyst", SystemPrompt = "You are a technical analyst. Analyze the technical aspects, feasibility, and implementation details. Keep to 150 words." },
				new() { Id = "market-analyst", Name = "market-analyst", SystemPrompt = "You are a market analyst. Analyze the market opportunity, competition, and business potential. Keep to 150 words." },
				new() { Id = "risk-analyst", Name = "risk-analyst", SystemPrompt = "You are a risk analyst. Identify potential risks, challenges, and mitigation strategies. Keep to 150 words." },
				new() { Id = "synthesizer", Name = "synthesizer", SystemPrompt = "You are a synthesis expert. Combine multiple analysis reports into a coherent executive briefing of 200 words or less." }
			]
		});

		services.AddDevUIWorkflow(new WorkflowInfo
		{
			Id = "handoff-support",
			Name = "Customer Support",
			Description = "Triage agent routes to the right specialist based on issue",
			Kind = Microsoft.Maui.AI.Agents.DevUI.OrchestrationKind.Handoff,
			DemoPrompt = "I need to return a defective laptop that keeps crashing",
			Executors =
			[
				new() { Id = "triage", Name = "triage", SystemPrompt = "You are a customer support triage agent. Analyze the issue and respond with ROUTE:billing, ROUTE:technical, or ROUTE:account followed by a brief explanation." },
				new() { Id = "billing", Name = "billing", SystemPrompt = "You are a billing specialist. Help with payment issues, subscriptions, refunds. Be empathetic. Under 200 words." },
				new() { Id = "technical", Name = "technical", SystemPrompt = "You are a technical support specialist. Help debug issues and provide step-by-step solutions. Under 200 words." },
				new() { Id = "account", Name = "account", SystemPrompt = "You are an account specialist. Help with login issues, password resets, account recovery. Under 200 words." }
			]
		});

		services.AddDevUIWorkflow(new WorkflowInfo
		{
			Id = "groupchat-design",
			Name = "Design Review",
			Description = "Designer, Engineer, and PM collaborate on a feature",
			Kind = Microsoft.Maui.AI.Agents.DevUI.OrchestrationKind.GroupChat,
			DemoPrompt = "Design a mobile banking app for Gen Z users",
			Executors =
			[
				new() { Id = "designer", Name = "designer", SystemPrompt = "You are a UX designer. Evaluate from usability and user experience perspective. Keep to 100 words." },
				new() { Id = "engineer", Name = "engineer", SystemPrompt = "You are a software engineer. Evaluate from technical feasibility and performance perspective. Keep to 100 words." },
				new() { Id = "product-manager", Name = "product-manager", SystemPrompt = "You are a product manager. Evaluate from business value and timeline perspective. Summarize decisions. Keep to 100 words." }
			]
		});
	}
}
