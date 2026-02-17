using PLang.Runtime2.Engine;

namespace PLang.Runtime2.Engine.Context;

public sealed class EventScope
{
    public EngineEvents Events { get; } = new();
}
