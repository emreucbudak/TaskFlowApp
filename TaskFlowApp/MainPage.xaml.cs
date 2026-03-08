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

    private void OnEntryHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            InputChromeHelper.RemoveNativeChrome(element.Handler?.PlatformView);
        }
    }
}
