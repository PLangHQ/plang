namespace PLang.Runtime2.Engine;

public sealed class Lifecycle
{
    public Bindings Before { get; } = new();
    public Bindings After { get; } = new();
}
