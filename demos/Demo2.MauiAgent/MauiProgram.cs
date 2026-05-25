using Demo.Orchestrations;
using Demo2.MauiAgent.Services;
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

		// Register DevUI with agent/workflow discovery from shared orchestrations
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
		// Register standalone agents from shared definitions
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

		// Register workflows from shared definitions
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
	}
}
