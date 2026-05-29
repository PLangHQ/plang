using app.actor.context;
using app.@event;
using app.variable;
using Action = app.goal.steps.step.actions.action.@this;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.@event.lifecycle.binding.list;

public sealed class @this
{
    private readonly List<EventBinding> _bindings = new();
    public int Count => _bindings.Count;

    public void Add(EventBinding binding) => _bindings.Add(binding);

    public bool Remove(EventBinding binding) => _bindings.Remove(binding);

    public void Clear() => _bindings.Clear();

    public IReadOnlyList<EventBinding> ToList() => _bindings.ToList();

    public Task<data.@this> Run(actor.context.@this context) => RunBindings(_bindings, context, null, null);

    /// <summary>
    /// Dispatches matching bindings. Payload-carrying events (AfterAction) pass the
    /// action and its result; other events leave action/result null.
    /// </summary>
    public Task<data.@this> Run(actor.context.@this context, EventType type,
        Action? action = null, data.@this? result = null)
        => RunBindings(_bindings.Where(b => b.Type == type), context, action, result);

    private static async Task<data.@this> RunBindings(IEnumerable<EventBinding> bindings,
        actor.context.@this context, Action? action, data.@this? actionResult)
    {
        foreach (var binding in bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context, action, actionResult);
            if (!result.Success) return result;
            if (result.Handled) return result;
        }
        return data.@this.Ok();
    }
}
