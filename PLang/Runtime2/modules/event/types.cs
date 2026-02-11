namespace PLang.Runtime2.modules.@event.types;

public record @event
{
    public string id { get; init; } = "";
    public string type { get; init; } = "";
    public string goalToCall { get; init; } = "";
    public string? pattern { get; init; }
    public bool isRegex { get; init; }
}
