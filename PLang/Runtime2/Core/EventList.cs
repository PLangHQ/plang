using PLang.Runtime2.Context;

namespace PLang.Runtime2.Core;

public sealed class EventList
{
    private readonly List<EventBinding> _bindings = new();
    public int Count => _bindings.Count;

    public void Add(EventBinding binding) => _bindings.Add(binding);

    public bool Remove(EventBinding binding) => _bindings.Remove(binding);

    public void Clear() => _bindings.Clear();

    public IReadOnlyList<EventBinding> ToList() => _bindings.ToList();

    public async Task<Return> Run(PLangContext context)
    {
        if (_bindings.Count == 0) return new Return();
        foreach (var binding in _bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Handler(context);
            if (!result.Success && binding.StopOnError) return result;
        }
        return new Return();
    }
}

public sealed class ObjectEvents
{
    public EventList Before { get; } = new();
    public EventList After { get; } = new();

    public void Add(EventBinding binding)
    {
        if (binding.Type is EventType.BeforeGoal or EventType.BeforeStep)
            Before.Add(binding);
        else
            After.Add(binding);
    }
}
