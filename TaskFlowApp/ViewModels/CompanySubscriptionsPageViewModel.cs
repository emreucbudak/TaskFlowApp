using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanySubscriptionsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    [RelayCommand]
    private Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CancelSubscriptionAsync()
    {
        ErrorMessage = string.Empty;
        StatusText = "Abonelik iptal talebiniz alindi. Islem icin yonetici onayi gereklidir.";
        return Task.CompletedTask;
    }
}
