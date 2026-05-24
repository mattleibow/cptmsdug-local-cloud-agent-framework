using Demo2.MauiAgent.ViewModels;

namespace Demo2.MauiAgent;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
