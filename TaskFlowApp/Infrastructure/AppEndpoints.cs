namespace TaskFlowApp.Infrastructure;

public static class AppEndpoints
{
#if ANDROID
    public const string ApiBaseUrl = "http://10.0.2.2:8080/";
#else
    public const string ApiBaseUrl = "http://localhost:8080/";
#endif

    public const string ChatHubPath = "chatHub";
    public const string NotificationHubPath = "notificationHub";
}
