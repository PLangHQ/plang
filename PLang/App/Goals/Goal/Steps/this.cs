using System.Collections;
using App.Actor.Context;
using App.modules;

namespace App.Goals.Goal.Steps;

public sealed class @this : IList<Step.@this>, IContext
{
    private readonly List<Step.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<Step.@this> steps) { _items = new List<Step.@this>(steps); }

    public Actor.Context.@this Context { get; set; } = null!;

    [System.Text.Json.Serialization.JsonIgnore]
    public Goal.@this? Goal { get; set; }

    public List<Step.@this> Value => _items;

    // --- IList<Step> implementation ---

    public Step.@this this[int index]
    {
        get { var s = _items[index]; s.Goal ??= Goal; return s; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Step.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<Step.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Step.@this item) => _items.Contains(item);
    public void CopyTo(Step.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Step.@this item) => _items.IndexOf(item);
    public void Insert(int index, Step.@this item) => _items.Insert(index, item);
    public bool Remove(Step.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>
    /// Custom enumerator that stamps context on each step and skips disabled steps.
    /// The condition module marks indented sub-steps as disabled when a condition is false.
    /// </summary>
    public IEnumerator<Step.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var step = _items[i];
            step.Goal ??= Goal;
            step.Context = Context;

            if (step.Disabled)
            {
                // Clear the disabled flag after skipping so re-execution works
                step.Disabled = false;
                continue;
            }

            yield return step;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns true if the step at the given index has a following step with higher indent.
    /// </summary>
    internal bool HasIndentedChildren(int index)
    {
        return index + 1 < _items.Count && _items[index + 1].Indent > _items[index].Indent;
    }

}
