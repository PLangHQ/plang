namespace PLang.Runtime2.modules.assert.types;

public record assert
{
    public bool success { get; init; }
    public string? message { get; init; }
}
