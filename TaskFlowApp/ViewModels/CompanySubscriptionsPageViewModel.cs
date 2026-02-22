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
}
