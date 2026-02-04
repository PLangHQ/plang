namespace PLang.Runtime;

public readonly partial struct GoalResult
{
    public string Type { get; }
    public object? Data { get; }
    public ChannelData Channels { get; }
    
    public bool IsSuccess => Type == "goal";
    public bool IsError => Type == "error";
    
    public GoalResult(string type, object? data, ChannelData? channels = null)
    {
        Type = type;
        Data = data;
        Channels = channels ?? new ChannelData();
    }
    
    public static GoalResult Success(object? data = null)
        => new("goal", data);
    
    public static GoalResult Error(string message, Exception? ex = null, int statusCode = 500)
        => new("error", null, new ChannelData { Error = new ErrorInfo(message, ex, statusCode) });
    
    public static GoalResult Error(ErrorInfo error)
        => new("error", null, new ChannelData { Error = error });
    
    // Task helpers - for clean returns
    public Task<GoalResult> AsTask() => Task.FromResult(this);
    
    public static Task<GoalResult> SuccessTask(object? data = null)
        => Task.FromResult(Success(data));
    
    public static Task<GoalResult> ErrorTask(string message, int statusCode = 500)
        => Task.FromResult(Error(message, statusCode: statusCode));
    
    public static Task<GoalResult> ErrorTask(string message, Exception ex, int statusCode = 500)
        => Task.FromResult(Error(message, ex, statusCode));
}
