namespace TaskFlowApp.Infrastructure.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToRootAsync(string route)
    {
        return Shell.Current.GoToAsync($"///{route}");
    }
}
