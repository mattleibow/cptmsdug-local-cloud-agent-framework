using Demo.Orchestrations;
using Demo2.MauiAgent.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Agents.DevUI;
using Microsoft.Maui.LifecycleEvents;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Extensions.Logging;
#endif

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

#if MACCATALYST
		// Minimize the native title bar on Mac Catalyst
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddiOS(ios => ios.SceneWillConnect((scene, session, options) =>
			{
				if (scene is UIKit.UIWindowScene windowScene)
				{
					var titlebar = windowScene.Titlebar;
					if (titlebar is not null)
					{
						titlebar.TitleVisibility = UIKit.UITitlebarTitleVisibility.Hidden;
						titlebar.Toolbar = null;
					}
				}
			}));
		});
#endif

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

		// Register DevUI (auto-discovers agents and workflows from DI)
		builder.Services.AddMauiAgentDevUI();

		// Register optional demo metadata (descriptions, demo prompts for UI)
		foreach (var wf in DemoWorkflows.Workflows)
		{
			builder.Services.AddDevUIWorkflowMetadata(wf.Id, wf.Description, wf.DemoPrompt);
		}

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

}
