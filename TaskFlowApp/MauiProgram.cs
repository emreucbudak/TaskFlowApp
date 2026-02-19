using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Pages;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IUserSession, UserSession>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IApiClient>(serviceProvider =>
        {
            var userSession = serviceProvider.GetRequiredService<IUserSession>();
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5172/"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            return new ApiClient(httpClient, userSession);
        });

        builder.Services.AddSingleton<IdentityApiClient>();
        builder.Services.AddSingleton<ProjectManagementApiClient>();
        builder.Services.AddSingleton<ChatApiClient>();
        builder.Services.AddSingleton<NotificationApiClient>();
        builder.Services.AddSingleton<ReportApiClient>();
        builder.Services.AddSingleton<StatsApiClient>();
        builder.Services.AddSingleton<TenantApiClient>();

        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<DashBoardPageViewModel>();
        builder.Services.AddTransient<ReportsPageViewModel>();
        builder.Services.AddTransient<TasksPageViewModel>();
        builder.Services.AddTransient<MessagesPageViewModel>();
        builder.Services.AddTransient<NotificationsPageViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashBoardPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<NotificationsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceLocator.Initialize(app.Services);
        return app;
    }
}
