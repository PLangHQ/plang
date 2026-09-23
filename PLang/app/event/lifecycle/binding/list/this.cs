using app.actor.context;
using app.@event;
using app.variable;
using Action = app.goal.step.action.@this;
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

    public Task<data.@this> Run(actor.context.@this context) => RunBindings(_bindings, context, null);

    /// <summary>
    /// Dispatches the bindings for the moment that fired — built by the node it fired on (a goal,
    /// step or action passes itself, an action its result too).
    /// </summary>
    public Task<data.@this> Run(actor.context.@this context, global::app.@event.moment.@this moment)
        => RunBindings(_bindings.Where(b => b.Type == moment.Trigger), context, moment);

    private static async Task<data.@this> RunBindings(IEnumerable<EventBinding> bindings,
        actor.context.@this context, global::app.@event.moment.@this? moment)
    {
        foreach (var binding in bindings.OrderByDescending(b => b.Priority))
        {
            var result = await binding.Run(context, moment);
            if (!result.Success) return result;
            if (result.Handled) return result;
        }
        return context.Ok();
    }
}
