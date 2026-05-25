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

		// Register standalone agents
		builder.AddStandaloneAgents();

		// Register all workflows (each self-contained, same as web app)
		builder.AddSequentialWorkflow();
		builder.AddConcurrentWorkflow();
		builder.AddHandoffWorkflow();
		builder.AddGroupChatWorkflow();

		// Register DevUI (auto-discovers agents and workflows from DI)
		builder.Services.AddMauiAgentDevUI();

		// Optional: demo prompts for the MAUI DevUI (not needed for discovery)
		builder.Services.AddDevUIWorkflowMetadata("sequential-newsdesk",
			"Reporter writes, Fact-Checker verifies, Editor polishes",
			"Write a news article about a breakthrough in fusion energy");
		builder.Services.AddDevUIWorkflowMetadata("concurrent-travel",
			"Multiple specialists plan in parallel, coordinator assembles itinerary",
			"Plan a 5-day trip to Tokyo for a food-loving couple");
		builder.Services.AddDevUIWorkflowMetadata("handoff-helpdesk",
			"Dispatcher routes tickets to the right IT specialist",
			"My VPN keeps disconnecting every 10 minutes and I can't access the internal wiki");
		builder.Services.AddDevUIWorkflowMetadata("groupchat-startup",
			"Founder pitches, Investor challenges, Advisor mediates",
			"Pitch an AI-powered personal finance app that uses on-device models for privacy");

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

}
