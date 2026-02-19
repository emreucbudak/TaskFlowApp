using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = ServiceLocator.GetRequiredService<MainPageViewModel>();
    }
}
