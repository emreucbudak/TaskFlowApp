using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MauiIcons.Material.Outlined;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Pages;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Services.State;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMaterialOutlinedMauiIcons()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Poppins-Regular.ttf", "PoppinsRegular");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Entry, Microsoft.Maui.Handlers.EntryHandler>();
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
                {
                    handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                });
                handlers.AddHandler<Editor, Microsoft.Maui.Handlers.EditorHandler>();
                Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
                {
                    handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                });
#elif WINDOWS
                handlers.AddHandler<Entry, Microsoft.Maui.Handlers.EntryHandler>();
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
                {
                    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    handler.PlatformView.Style = null;
                    var res = handler.PlatformView.Resources;
                    res["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                    handler.PlatformView.Resources = res;
                });
                handlers.AddHandler<Editor, Microsoft.Maui.Handlers.EditorHandler>();
                Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
                {
                    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    handler.PlatformView.Style = null;
                    var res = handler.PlatformView.Resources;
                    res["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                    handler.PlatformView.Resources = res;
                });
#endif
            });

        builder.Services.AddSingleton<IUserSession, UserSession>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IApiClient>(serviceProvider =>
        {
            var userSession = serviceProvider.GetRequiredService<IUserSession>();
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(AppEndpoints.ApiBaseUrl),
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
        builder.Services.AddSingleton<AiApiClient>();
        builder.Services.AddSingleton<IWorkerReportAccessResolver, WorkerReportAccessResolver>();
        builder.Services.AddSingleton<IWorkerDashboardStateService, WorkerDashboardStateService>();
        builder.Services.AddSingleton<ISignalRChatService, SignalRChatService>();
        builder.Services.AddSingleton<ISignalRNotificationService, SignalRNotificationService>();
        builder.Services.AddSingleton<IRealtimeConnectionManager, RealtimeConnectionManager>();

        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<DashBoardPageViewModel>();
        builder.Services.AddTransient<GroupDetailsPageViewModel>();
        builder.Services.AddTransient<ProfilePageViewModel>();
        builder.Services.AddTransient<CompanyDashboardPageViewModel>();
        builder.Services.AddTransient<CompanyReportsPageViewModel>();
        builder.Services.AddTransient<CompanyTasksPageViewModel>();
        builder.Services.AddTransient<CompanyEmployeesPageViewModel>();
        builder.Services.AddTransient<CompanySubscriptionsPageViewModel>();
        builder.Services.AddTransient<ReportsPageViewModel>();
        builder.Services.AddTransient<TasksPageViewModel>();
        builder.Services.AddTransient<AllIndividualTasksPageViewModel>();
        builder.Services.AddTransient<LeaderIndividualTaskPageViewModel>();
        builder.Services.AddTransient<MessagesPageViewModel>();
        builder.Services.AddTransient<NotificationsPageViewModel>();
        builder.Services.AddTransient<CreateReportPageViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashBoardPage>();
        builder.Services.AddTransient<GroupDetailsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<CompanyDashboardPage>();
        builder.Services.AddTransient<CompanyReportsPage>();
        builder.Services.AddTransient<CompanyTasksPage>();
        builder.Services.AddTransient<CompanyEmployeesPage>();
        builder.Services.AddTransient<CompanySubscriptionsPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<AllIndividualTasksPage>();
        builder.Services.AddTransient<LeaderIndividualTaskPage>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<CreateReportPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        return app;
    }
}
