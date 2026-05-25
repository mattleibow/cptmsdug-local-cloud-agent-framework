using Demo.Orchestrations;
using Demo2.MauiAgent.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Agents.DevUI;
using Microsoft.Maui.DevFlow.Agent;
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

		// Register standalone agents (same pattern as web app)
		foreach (var agent in DemoWorkflows.StandaloneAgents)
		{
			builder.AddAIAgent(agent.Name, agent.SystemPrompt);
		}

		// Register all workflows (each in its own file, same as web app)
		builder.AddSequentialWorkflow();
		builder.AddConcurrentWorkflow();
		builder.AddHandoffWorkflow();
		builder.AddGroupChatWorkflow();

		// Register DevUI with entities from shared definitions
		builder.Services.AddMauiAgentDevUI();
		builder.Services.AddDemoDevUIEntities();

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>
	/// Maps Demo.Orchestrations definitions into DevUI display entities.
	/// </summary>
	private static IServiceCollection AddDemoDevUIEntities(this IServiceCollection services)
	{
		foreach (var agent in DemoWorkflows.StandaloneAgents)
		{
			services.AddDevUIAgent(new AgentInfo
			{
				Id = agent.Name,
				Name = agent.Name,
				Description = agent.SystemPrompt[..Math.Min(80, agent.SystemPrompt.Length)] + "...",
				Instructions = agent.SystemPrompt
			});
		}

		foreach (var wf in DemoWorkflows.Workflows)
		{
			services.AddDevUIWorkflow(new WorkflowInfo
			{
				Id = wf.Id,
				Name = wf.Name,
				Description = wf.Description,
				Kind = (Microsoft.Maui.AI.Agents.DevUI.OrchestrationKind)(int)wf.Kind,
				DemoPrompt = wf.DemoPrompt,
				Executors = wf.Agents.Select(a => new ExecutorInfo
				{
					Id = a.Name,
					Name = a.Name,
					SystemPrompt = a.SystemPrompt
				}).ToList()
			});
		}

		return services;
	}
}
