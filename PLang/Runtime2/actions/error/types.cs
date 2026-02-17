namespace PLang.Runtime2.actions.error.types;

public record error
{
    public string message { get; init; } = "";
    public string key { get; init; } = "";
    public int statusCode { get; init; }
}
