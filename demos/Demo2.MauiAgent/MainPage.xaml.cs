using Microsoft.Maui.AI.Agents.DevUI;

namespace Demo2.MauiAgent;

public partial class MainPage : ContentPage
{
	public MainPage(IDevUIEntityRegistry registry, IServiceProvider services)
	{
		Title = "Agent Framework Dev UI";

		var devUI = new AgentDevUIView
		{
			EntityRegistry = registry
		};

		Content = devUI;
	}
}
