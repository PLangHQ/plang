using System.Collections;
using PrAction = global::app.goal.steps.step.actions.action.@this;

namespace app.goal.steps.step.actions.action.modifiers;

/// <summary>
/// Ordered list of modifier actions attached to an Action.
/// Owns the right-to-left fold that wraps an inner operation at runtime —
/// first in the list becomes the outermost wrapper.
/// </summary>
public sealed class @this : IList<PrAction>
{
    private readonly List<PrAction> _items = new();

    public @this() { }
    public @this(IEnumerable<PrAction> items) { _items = new List<PrAction>(items); }

    public PrAction this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(PrAction item) => _items.Add(item);
    public void AddRange(IEnumerable<PrAction> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(PrAction item) => _items.Contains(item);
    public void CopyTo(PrAction[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(PrAction item) => _items.IndexOf(item);
    public void Insert(int index, PrAction item) => _items.Insert(index, item);
    public bool Remove(PrAction item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    public IEnumerator<PrAction> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Runs an inner operation wrapped by this collection right-to-left.
    /// Each action resolves its own handler via WrapAround; this collection
    /// owns the iteration order.
    /// After the chain completes, fires AfterAction for each modifier so
    /// coverage tracks modifier presence (architect §5.6). Modifiers don't go
    /// through Action.RunAsync (they're wrapping, not standalone executables),
    /// so we emit the event here — once per modifier, carrying the chain's result.
    /// </summary>
    public async Task<data.@this> RunAsync(
        Func<Task<data.@this>> innermost,
        actor.context.@this context)
    {
        if (_items.Count == 0) return await innermost();

        var execute = innermost;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var (wrapped, error) = await _items[i].WrapAround(execute, context);
            if (error != null) return context.Error(error);
            execute = wrapped!;
        }
        var result = await execute();

        foreach (var modifier in _items)
        {
            var lifecycle = context.LifecycleFor(modifier);
            await lifecycle.After.Run(context, app.@event.Trigger.AfterAction, modifier, result);
        }

        return result;
    }
}
