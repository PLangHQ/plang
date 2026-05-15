using app.Actor.Context;
using app.Events;
using app.Variables;
using Action = app.Goals.Goal.Steps.Step.Actions.Action.@this;
using EventBinding = app.Events.Lifecycle.Bindings.Binding.@this;

namespace app.Events.Lifecycle.Bindings;

public sealed class @this
{
    private readonly List<EventBinding> _bindings = new();
    public int Count => _bindings.Count;

    public void Add(EventBinding binding) => _bindings.Add(binding);

    public bool Remove(EventBinding binding) => _bindings.Remove(binding);

    public void Clear() => _bindings.Clear();

    public IReadOnlyList<EventBinding> ToList() => _bindings.ToList();

    public Task<Data.@this> Run(Actor.Context.@this context) => RunBindings(_bindings, context, null, null);

    /// <summary>
    /// Dispatches matching bindings. Payload-carrying events (AfterAction) pass the
    /// action and its result; other events leave action/result null.
    /// </summary>
    public Task<Data.@this> Run(Actor.Context.@this context, EventType type,
        Action? action = null, Data.@this? result = null)
        => RunBindings(_bindings.Where(b => b.Type == type), context, action, result);

    private static async Task<Data.@this> RunBindings(IEnumerable<EventBinding> bindings,
        Actor.Context.@this context, Action? action, Data.@this? actionResult)
    {
        foreach (var binding in bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context, action, actionResult);
            if (!result.Success) return result;
            if (result.Handled) return result;
        }
        return Data.@this.Ok();
    }
}
