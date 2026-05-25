using Microsoft.Maui.AI.Agents.DevUI;

namespace Demo2.MauiAgent;

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
