using R2Bindings = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.@this;

namespace PLang.Runtime2.Engine.Events.Lifecycle;

public sealed class @this
{
    public R2Bindings Before { get; } = new();
    public R2Bindings After { get; } = new();
}
