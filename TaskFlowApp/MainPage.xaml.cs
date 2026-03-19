using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnEntryHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            InputChromeHelper.RemoveNativeChrome(element.Handler?.PlatformView);
        }
    }
}
