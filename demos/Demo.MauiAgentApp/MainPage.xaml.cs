using Microsoft.Maui.AI.Agents.DevUI;

namespace Demo.MauiAgentApp;

public partial class MainPage : ContentPage
{
	public MainPage(IDevUIEntityRegistry registry, IServiceProvider services)
	{
		var devUI = new AgentDevUIView
		{
			EntityRegistry = registry
		};

		Content = devUI;
	}
}
