namespace TaskFlowApp.Infrastructure.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route)
    {
        return Shell.Current.GoToAsync(route, false);
    }

    public Task GoToRootAsync(string route)
    {
        return Shell.Current.GoToAsync($"///{route}", false);
    }

    public Task GoBackAsync()
    {
        return Shell.Current.GoToAsync("..", false);
    }
}
