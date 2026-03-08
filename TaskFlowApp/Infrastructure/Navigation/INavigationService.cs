namespace TaskFlowApp.Infrastructure.Navigation;

public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToRootAsync(string route);
    Task GoBackAsync();
}