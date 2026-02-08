namespace PLang.Runtime2.actions.variable.types;

public record variable
{
    public string name { get; init; } = "";
    public object? value { get; init; }
    public string? type { get; init; }
    public bool exists { get; init; }
}
