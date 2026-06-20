namespace error;

// A failed Data carries one of these — the what and the where.
public sealed class @this(string message, string key = "Error")
{
    public string Message { get; } = message;
    public string Key { get; } = key;
}
