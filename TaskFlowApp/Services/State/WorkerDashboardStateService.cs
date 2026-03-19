using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.Services.State;

public sealed class WorkerDashboardStateService(
    IUserSession userSession,
    AiApiClient aiApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver) : IWorkerDashboardStateService
{
    private static readonly TimeSpan SummaryCacheDuration = TimeSpan.FromMinutes(15);

    private readonly SemaphoreSlim summaryLock = new(1, 1);
    private DashboardSummaryCacheEntry? summaryEntry;
    private Task<string?>? inFlightSummaryTask;
    private Guid? unreadCountUserId;
    private int? unreadMessageCount;

    public bool TryGetCachedDailySummary(out string summary)
    {
        summary = string.Empty;
        if (!TryResolveCurrentWorker(out var userId, out var companyId))
        {
            return false;
        }

        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var entry = summaryEntry;
        if (entry is null ||
            entry.UserId != userId ||
            entry.CompanyId != companyId ||
            entry.Date != currentDate ||
            DateTimeOffset.UtcNow - entry.CachedAtUtc > SummaryCacheDuration ||
            string.IsNullOrWhiteSpace(entry.Summary))
        {
            return false;
        }

        summary = entry.Summary;
        return true;
    }

    public Task WarmDailySummaryAsync(CancellationToken cancellationToken = default) =>
        GetOrLoadDailySummaryAsync(cancellationToken);

    public async Task<string?> GetOrLoadDailySummaryAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCachedDailySummary(out var cachedSummary))
        {
            return cachedSummary;
        }

        Task<string?> fetchTask;
        await summaryLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedDailySummary(out cachedSummary))
            {
                return cachedSummary;
            }

            inFlightSummaryTask ??= FetchDailySummaryCoreAsync();
            fetchTask = inFlightSummaryTask;
        }
        finally
        {
            summaryLock.Release();
        }

        return await fetchTask;
    }

    public bool TryGetUnreadMessageCount(out int unreadCount)
    {
        unreadCount = 0;
        if (userSession.UserId is null ||
            unreadCountUserId != userSession.UserId ||
            unreadMessageCount is null)
        {
            return false;
        }

        unreadCount = unreadMessageCount.Value;
        return true;
    }

    public void SetUnreadMessageCount(int unreadCount)
    {
        if (userSession.UserId is null)
        {
            unreadCountUserId = null;
            unreadMessageCount = null;
            return;
        }

        unreadCountUserId = userSession.UserId;
        unreadMessageCount = Math.Max(0, unreadCount);
    }

    public void IncrementUnreadMessageCount(int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        var currentCount = TryGetUnreadMessageCount(out var cachedUnreadCount)
            ? cachedUnreadCount
            : 0;

        SetUnreadMessageCount(currentCount + delta);
    }

    public void DecrementUnreadMessageCount(int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        var currentCount = TryGetUnreadMessageCount(out var cachedUnreadCount)
            ? cachedUnreadCount
            : 0;

        SetUnreadMessageCount(Math.Max(0, currentCount - delta));
    }

    public void Clear()
    {
        summaryEntry = null;
        inFlightSummaryTask = null;
        unreadCountUserId = null;
        unreadMessageCount = null;
    }

    private async Task<string?> FetchDailySummaryCoreAsync()
    {
        try
        {
            if (!TryResolveCurrentWorker(out var userId, out var companyId))
            {
                return null;
            }

            var accessState = await workerReportAccessResolver.GetStateAsync();
            var result = await aiApiClient.GetDailySummaryAsync(
                userId,
                companyId,
                accessState.CanAccessReportsPage,
                accessState.DepartmentId);

            var summary = NormalizeSummary(result?.Summary);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return null;
            }

            summaryEntry = new DashboardSummaryCacheEntry(
                userId,
                companyId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                summary,
                DateTimeOffset.UtcNow);

            return summary;
        }
        catch
        {
            return null;
        }
        finally
        {
            await summaryLock.WaitAsync();
            try
            {
                inFlightSummaryTask = null;
            }
            finally
            {
                summaryLock.Release();
            }
        }
    }

    private bool TryResolveCurrentWorker(out Guid userId, out Guid companyId)
    {
        userId = Guid.Empty;
        companyId = Guid.Empty;

        if (!string.Equals(userSession.Role, AppRoles.Worker, StringComparison.OrdinalIgnoreCase) ||
            userSession.UserId is null ||
            userSession.CompanyId is null)
        {
            return false;
        }

        userId = userSession.UserId.Value;
        companyId = userSession.CompanyId.Value;
        return true;
    }

    private static string? NormalizeSummary(string? summary)
    {
        return string.IsNullOrWhiteSpace(summary)
            ? null
            : summary.Trim();
    }
}

internal sealed record DashboardSummaryCacheEntry(
    Guid UserId,
    Guid CompanyId,
    DateOnly Date,
    string Summary,
    DateTimeOffset CachedAtUtc);
