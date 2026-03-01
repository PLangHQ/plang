namespace PLang.Runtime2.modules.error.types;

public record error
{
    public string message { get; init; } = "";
    public string key { get; init; } = "";
    public int statusCode { get; init; }
}
