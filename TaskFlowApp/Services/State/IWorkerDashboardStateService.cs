namespace TaskFlowApp.Services.State;

public interface IWorkerDashboardStateService
{
    bool TryGetCachedDailySummary(out string summary);
    Task WarmDailySummaryAsync(CancellationToken cancellationToken = default);
    Task<string?> GetOrLoadDailySummaryAsync(CancellationToken cancellationToken = default);
    bool TryGetUnreadMessageCount(out int unreadMessageCount);
    void SetUnreadMessageCount(int unreadMessageCount);
    void IncrementUnreadMessageCount(int delta = 1);
    void DecrementUnreadMessageCount(int delta = 1);
    void Clear();
}
