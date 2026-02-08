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
            var result = await binding.Handler(context);
            if (!result.Success && binding.StopOnError) return result;
        }
        return Data.Ok();
    }
}

public sealed class PhaseEvents
{
    public EventList Load { get; } = new();
    private readonly EventList _runtime = new();
    public Task<Data> Run(PLangContext context) => _runtime.Run(context);
    public void Add(EventBinding binding) => _runtime.Add(binding);
}

public sealed class EntityEvents
{
    public PhaseEvents Before { get; } = new();
    public PhaseEvents After { get; } = new();
}
