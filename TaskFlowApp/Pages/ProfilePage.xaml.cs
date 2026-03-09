using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class ProfilePage : ContentPage
{
    private ProfilePageViewModel ViewModel => (ProfilePageViewModel)BindingContext;

    public ProfilePage()
    {
        InitializeComponent();
        Shell.SetPresentationMode(this, PresentationMode.NotAnimated);
        BindingContext = ServiceLocator.GetRequiredService<ProfilePageViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
