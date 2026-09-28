namespace app.module.output.type;

public record output
{
    public object? content { get; init; }
    public string channel { get; init; } = "default";
}
