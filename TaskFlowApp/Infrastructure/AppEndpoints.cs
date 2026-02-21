namespace TaskFlowApp.Infrastructure;

public static class AppEndpoints
{
    public static string[] ApiBaseUrls =>
#if ANDROID
        ["http://10.0.2.2:8080/", "http://10.0.2.2:5172/"];
#else
        ["http://localhost:8080/", "http://localhost:5172/"];
#endif

    public static string ApiBaseUrl => ApiBaseUrls[0];

    public const string ChatHubPath = "chatHub";
    public const string NotificationHubPath = "notificationHub";
}
