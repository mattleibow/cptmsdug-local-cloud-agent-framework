using Demo.MauiAgentApp.Orchestrations;
using Demo.MauiAgentApp.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Agents.DevUI;
using Microsoft.Maui.LifecycleEvents;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
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

		// Configure OpenTelemetry → standalone Aspire Dashboard.
		// AddDemoTelemetry registers traces / metrics / logs and an
		// IMauiInitializeService that forces provider resolution after
		// the host is built (MAUI never starts hosted services).
		builder.AddDemoTelemetry("Demo.MauiAgentApp");

		// Open the log floodgates: default to Information, but route every
		// AI-related source up to Trace so prompts, responses, and tool
		// invocations land in the Aspire Dashboard alongside the spans.
		builder.Logging.SetMinimumLevel(LogLevel.Information);
		builder.Logging.AddFilter("Microsoft.Extensions.AI", LogLevel.Trace);
		builder.Logging.AddFilter("Microsoft.Agents.AI", LogLevel.Trace);
		builder.Logging.AddFilter("Demo.MauiAgentApp", LogLevel.Trace);

		// Register services
		builder.Services.AddSingleton<AIChatService>();
		// Register both keyed IChatClients for the local + cloud demo.
		//   - "cloud-model" → Azure OpenAI (AIChatService)
		//   - "local-model" → Apple Intelligence Foundation Models on
		//                     iOS / macOS / macCatalyst 26+
		// Other platforms fall back to the cloud client for the local key
		// so the workflows still register (with the obvious privacy
		// caveat — this is dev-only).
		//
		// Both clients are wrapped in .UseOpenTelemetry(...) with
		// EnableSensitiveData = true so prompt/response content shows up in
		// the spans on the Aspire Dashboard. Dev only — never ship this
		// with real user data in the pipeline.
		builder.Services.AddKeyedSingleton<IChatClient>(AIModels.Cloud, (sp, _) =>
			sp.GetRequiredService<AIChatService>().ChatClient
				.AsBuilder()
				.UseOpenTelemetry(
					loggerFactory: sp.GetService<ILoggerFactory>(),
					sourceName: "Demo.MauiAgentApp",
					configure: o => o.EnableSensitiveData = true)
				.Build(sp));

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
				.UseOpenTelemetry(
					loggerFactory: sp.GetService<ILoggerFactory>(),
					sourceName: "Demo.MauiAgentApp",
					configure: o => o.EnableSensitiveData = true)
				.Build(sp));
#pragma warning restore CA1416, MAUIAI0001
#else
		// Fallback: on platforms without on-device AI, route "local-model"
		// to the same cloud client. Only useful for development on Windows
		// / Android. Production demos run on macCatalyst 26+.
		builder.Services.AddKeyedSingleton<IChatClient>(AIModels.Local, (sp, _) =>
			sp.GetRequiredService<AIChatService>().ChatClient
				.AsBuilder()
				.UseOpenTelemetry(
					loggerFactory: sp.GetService<ILoggerFactory>(),
					sourceName: "Demo.MauiAgentApp",
					configure: o => o.EnableSensitiveData = true)
				.Build(sp));
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

		var app = builder.Build();

		return app;
	}
}
