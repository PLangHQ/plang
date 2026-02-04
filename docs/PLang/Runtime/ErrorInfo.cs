namespace PLang.Runtime;

public readonly partial struct ErrorInfo
{
    public string Message { get; }
    public string? StackTrace { get; }
    public string? Type { get; }
    public int StatusCode { get; }
    
    public ErrorInfo(string message, Exception? ex = null, int statusCode = 500)
    {
        Message = message;
        StackTrace = ex?.StackTrace;
        Type = ex?.GetType().Name;
        StatusCode = statusCode;
    }
    
    public override string ToString()
    {
        return $"[{StatusCode}] {Type}: {Message}";
    }
}
