using App.Actor.Context;
using App.Events;
using App.Variables;
using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.Events.Lifecycle.Bindings;

public sealed class @this
{
    private readonly List<EventBinding> _bindings = new();
    public int Count => _bindings.Count;

    public void Add(EventBinding binding) => _bindings.Add(binding);

    public bool Remove(EventBinding binding) => _bindings.Remove(binding);

    public void Clear() => _bindings.Clear();

    public IReadOnlyList<EventBinding> ToList() => _bindings.ToList();

    public Task<Data.@this> Run(Actor.Context.@this context) => RunBindings(_bindings, context);

    public Task<Data.@this> Run(Actor.Context.@this context, EventType type)
        => RunBindings(_bindings.Where(b => b.Type == type), context);

    private static async Task<Data.@this> RunBindings(IEnumerable<EventBinding> bindings, Actor.Context.@this context)
    {
        foreach (var binding in bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context);
            if (!result.Success) return result;
            if (result.Handled) return result;
        }
        return Data.@this.Ok();
    }
}
