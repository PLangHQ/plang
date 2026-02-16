using PLang.Runtime2;

namespace PLang.Runtime2.Context;

public sealed class EventScope
{
    public Events Events { get; } = new();
}
