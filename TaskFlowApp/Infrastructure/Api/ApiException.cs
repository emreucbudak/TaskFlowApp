namespace TaskFlowApp.Infrastructure.Api;

public sealed class ApiException : Exception
{
    public ApiException(string message, int statusCode, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }
    public string? ResponseBody { get; }
}
