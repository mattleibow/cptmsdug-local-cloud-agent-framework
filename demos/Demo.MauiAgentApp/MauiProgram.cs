using Demo.Orchestrations;
using Demo.MauiAgentApp.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Agents.DevUI;
using Microsoft.Maui.LifecycleEvents;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Extensions.Logging;
#endif

namespace Demo.MauiAgentApp;

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
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
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

		// Configure OpenTelemetry → Aspire Dashboard
		builder.Services.AddDemoTelemetry("Demo.MauiAgentApp");

		// Register services
		builder.Services.AddSingleton<AIChatService>();
		// Register both keyed IChatClients for the local + cloud demo.
		//   - "cloud-model" → Azure OpenAI (AIChatService)
		//   - "local-model" → Apple Intelligence Foundation Models on
		//                     iOS / macOS / macCatalyst 26+
		// Other platforms fall back to the cloud client for the local key
		// so the workflows still register (with the obvious privacy
		// caveat — this is dev-only).
		builder.Services.AddKeyedSingleton<IChatClient>(AIModels.Cloud, (sp, _) =>
			sp.GetRequiredService<AIChatService>().ChatClient);

#if IOS || MACCATALYST
#pragma warning disable CA1416, MAUIAI0001 // iOS / macCatalyst 26.0 + experimental Apple Intelligence API
		// Real on-device inference via Apple Intelligence Foundation Models.
		// Requires iOS / macCatalyst 26+ on Apple silicon. Comment this
		// branch out and use the cloud fallback below if you want to debug
		// the workflow shape without the on-device model in the loop.
		builder.Services.AddKeyedSingleton<IChatClient>(AIModels.Local, (sp, _) =>
			new Microsoft.Maui.Essentials.AI.AppleIntelligenceChatClient()
				.AsBuilder()
				.UseFunctionInvocation()
				.Build(sp));
#pragma warning restore CA1416, MAUIAI0001
#else
		// Fallback: on platforms without on-device AI, route "local-model"
		// to the same cloud client. Only useful for development on Windows
		// / Android. Production demos run on macCatalyst 26+.
		builder.Services.AddKeyedSingleton<IChatClient>(AIModels.Local, (sp, _) =>
			sp.GetRequiredService<AIChatService>().ChatClient);
#endif

		// Default (un-keyed) IChatClient — points at the cloud model for
		// any standalone agent or tool that doesn't specify a key.
		builder.Services.AddSingleton<IChatClient>(sp =>
			sp.GetRequiredKeyedService<IChatClient>(AIModels.Cloud));

		// Register standalone agents
		builder.AddStandaloneAgents();

		// Register all workflows (each self-contained, same as web app)
		builder.AddSequentialWorkflow();
		builder.AddConcurrentWorkflow();
		builder.AddHandoffWorkflow();
		builder.AddGroupChatWorkflow();
		builder.AddMeetingInviteWorkflow();

		// Register DevUI (auto-discovers agents and workflows from DI)
		builder.Services.AddMauiAgentDevUI();

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

}
