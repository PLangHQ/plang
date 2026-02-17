using PLang.Runtime2.Engine;

namespace PLang.Runtime2.Engine.Context;

public sealed class EventScope
{
    public Events Events { get; } = new();
}
