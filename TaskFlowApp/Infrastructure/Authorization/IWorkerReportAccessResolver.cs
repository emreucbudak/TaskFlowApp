namespace TaskFlowApp.Infrastructure.Authorization;

public interface IWorkerReportAccessResolver
{
    Task<WorkerReportAccessState> GetStateAsync(CancellationToken cancellationToken = default);
}

public sealed record WorkerReportAccessState(bool CanAccessReportsPage, Guid? DepartmentId, string DepartmentName)
{
    public static WorkerReportAccessState None { get; } = new(false, null, string.Empty);
}