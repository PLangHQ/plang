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
}
