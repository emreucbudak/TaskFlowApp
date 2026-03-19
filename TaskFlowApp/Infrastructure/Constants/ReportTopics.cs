namespace TaskFlowApp.Infrastructure.Constants;

public static class ReportTopics
{
    public const int BugReportId = 1;
    public const int FeedbackId = 2;
    public const int OtherId = 3;

    public const string BugReport = "Hata Bildirimi";
    public const string Feedback = "Geri Bildirim";
    public const string Other = "Diğer";

    public static readonly IReadOnlyList<string> All = [BugReport, Feedback, Other];

    public static string GetName(int topicId) => topicId switch
    {
        BugReportId => BugReport,
        FeedbackId => Feedback,
        OtherId => Other,
        _ => Other
    };

    public static int GetId(string topicName) => topicName switch
    {
        BugReport => BugReportId,
        Feedback => FeedbackId,
        _ => OtherId
    };
}
