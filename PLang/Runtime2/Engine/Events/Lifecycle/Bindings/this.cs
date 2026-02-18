using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Events;

public sealed class Bindings
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
            if (result.Handled) return result;
        }
        return Data.Ok();
    }

    public async Task<Data> Run(PLangContext context, EventType type)
    {
        var matching = _bindings.Where(b => b.Type == type).ToList();
        if (matching.Count == 0) return Data.Ok();
        foreach (var binding in matching.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context);
            if (!result.Success) return result;
            if (result.Handled) return result;
        }
        return Data.Ok();
    }
}
