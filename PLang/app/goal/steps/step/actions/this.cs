using System.Collections;

namespace app.goal.steps.step.actions;

public sealed partial class @this : IList<action.@this>
{
    // A plain C# collection host (list of action) — carried as clr<actions>, reflected as an
    // array of action hosts (the * kind's Output/Read). No item.@this base.
    private readonly List<action.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<action.@this> actions) { _items = new List<action.@this>(actions); }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step? Step { get; set; }

    public action.@this this[int index]
    {
        get { var a = _items[index]; a.Step ??= Step; return a; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(action.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<action.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(action.@this item) => _items.Contains(item);
    public void CopyTo(action.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(action.@this item) => _items.IndexOf(item);
    public void Insert(int index, action.@this item) => _items.Insert(index, item);
    public bool Remove(action.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    public IEnumerator<action.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<action.@this> Value => _items;

    /// <summary>
    /// Nests each modifier onto the preceding action's Modifiers slot — the flat LLM order becomes
    /// the .pr shape. The ROLE is answered by the CATALOG element (a modifier is a TYPE there, not a
    /// flag): the flat item, read as a plain action host, becomes the modifier it IS, with Order from
    /// the catalog. A leading modifier with no preceding action is dropped with a warning. Mutates
    /// in place. (The flat list carries modifiers only until the builder emits nested — then this dies.)
    /// </summary>
    public void Nest(global::app.module.list.@this modules)
    {
        if (_items.Count == 0) return;

        var flat = _items.ToList();
        _items.Clear();
        action.@this? current = null;

        foreach (var a in flat)
        {
            if (modules.Contains(a.Module)
                && modules[a.Module][a.ActionName] is action.modifier.@this catalog)
            {
                if (current == null)
                {
                    Step?.Warnings.Add(new Info
                    {
                        Key = "DroppedLeadingModifier",
                        Message = $"Modifier '{a.Module}.{a.ActionName}' has no preceding action and was dropped"
                    });
                    continue;
                }
                current.Modifiers.Add(new action.modifier.@this
                    { Module = a.Module, ActionName = a.ActionName, Parameters = a.Parameters, Position = catalog.Position });
            }
            else
            {
                current = a;
                _items.Add(a);
            }
        }

        foreach (var a in _items)
            a.Modifiers.Sort((x, y) => x.Position.CompareTo(y.Position));   // outermost wrapper (lowest Position) first
    }
}
