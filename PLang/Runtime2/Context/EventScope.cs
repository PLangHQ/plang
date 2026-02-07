using PLang.Runtime2.Core;

namespace PLang.Runtime2.Context;

public sealed class EventScope
{
    public EventCollection Events { get; } = new();
}
