using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanyEmployeesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
}
