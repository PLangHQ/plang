namespace PLang.Runtime2.modules.output.types;

public record output
{
    public object? content { get; init; }
    public string channel { get; init; } = "default";
}
