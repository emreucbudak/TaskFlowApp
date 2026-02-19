namespace TaskFlowApp.Infrastructure.Navigation;

public interface INavigationService
{
    Task GoToRootAsync(string route);
}
