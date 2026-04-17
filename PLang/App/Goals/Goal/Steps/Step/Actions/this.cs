using System.Collections;

namespace App.Goals.Goal.Steps.Step.Actions;

public sealed class @this : IList<Action.@this>
{
    private readonly List<Action.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<Action.@this> actions) { _items = new List<Action.@this>(actions); }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step.@this? Step { get; set; }

    public Action.@this this[int index]
    {
        get { var a = _items[index]; a.Step ??= Step; return a; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Action.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<Action.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Action.@this item) => _items.Contains(item);
    public void CopyTo(Action.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Action.@this item) => _items.IndexOf(item);
    public void Insert(int index, Action.@this item) => _items.Insert(index, item);
    public bool Remove(Action.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    public IEnumerator<Action.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<Action.@this> Value => _items;

    /// <summary>
    /// Takes a flat list where modifier actions follow their target action, and groups
    /// each modifier onto the preceding executable action's Modifiers collection.
    /// Modifiers are sorted by [Modifier(Order = N)] so the outermost wrapper comes first.
    /// A leading modifier with no preceding executable is dropped. Mutates in place.
    /// </summary>
    public void GroupModifiers(Modules.@this modules)
    {
        if (_items.Count == 0) return;

        var flat = _items.ToList();
        _items.Clear();
        Action.@this? current = null;

        foreach (var action in flat)
        {
            if (modules.IsModifier(action.Module, action.ActionName))
            {
                if (current == null)
                {
                    Step?.Warnings.Add(new Info
                    {
                        Key = "DroppedLeadingModifier",
                        Message = $"Modifier '{action.Module}.{action.ActionName}' has no preceding action and was dropped"
                    });
                    continue;
                }
                current.Modifiers.Add(action);
            }
            else
            {
                current = action;
                _items.Add(action);
            }
        }

        foreach (var action in _items)
        {
            if (action.Modifiers.Count <= 1) continue;
            var sorted = action.Modifiers
                .OrderBy(m => modules.GetModifierOrder(m.Module, m.ActionName))
                .ToList();
            action.Modifiers.Clear();
            foreach (var m in sorted) action.Modifiers.Add(m);
        }
    }
}
