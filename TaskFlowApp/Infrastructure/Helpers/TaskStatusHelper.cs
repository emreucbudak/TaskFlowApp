namespace TaskFlowApp.Infrastructure.Helpers;

public static class TaskStatusHelper
{
    public const string DefaultOpenStatus = "Acik";
    private const string CompletedStatus = "Tamamlandı";

    public static bool IsCompletedStatus(string? statusName)
    {
        return string.Equals(statusName?.Trim(), CompletedStatus, StringComparison.OrdinalIgnoreCase);
    }
}
