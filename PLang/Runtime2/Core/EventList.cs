using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed class EventList
{
    private readonly List<EventBinding> _bindings = new();
    public int Count => _bindings.Count;

    public void Add(EventBinding binding) => _bindings.Add(binding);

    public bool Remove(EventBinding binding) => _bindings.Remove(binding);

    public void Clear() => _bindings.Clear();

    public IReadOnlyList<EventBinding> ToList() => _bindings.ToList();

    public async Task<Data> Run(PLangContext context)
    {
        if (_bindings.Count == 0) return Data.Ok();
        foreach (var binding in _bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context);
            if (!result.Success) return result;
        }
        return Data.Ok();
    }
}

public sealed class BeforeAfterEvents
{
    public EventList Before { get; } = new();
    public EventList After { get; } = new();
}

public sealed class GoalStepEvents
{
    public BeforeAfterEvents Load { get; } = new();
    public EventList Before { get; } = new();
    public EventList After { get; } = new();
}

public sealed class ActionEvents
{
    public EventList Before { get; } = new();
    public EventList After { get; } = new();
}
