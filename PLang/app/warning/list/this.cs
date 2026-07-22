namespace app.warning.list;

/// <summary>
/// A program node's build diagnostics — its own collection of <see cref="app.warning.@this"/> rows
/// (private backing, own Add, read-only surface, per the naked-collection rule). Exposed behind the
/// singular <c>.Warning</c> property on goal/step/action. Reflection-serializable in Debug mode
/// (Count + indexer) exactly as the <c>List&lt;Info&gt;</c> it replaced; <c>[Debug]</c>-only on the
/// node, never on the <c>.pr</c> Store wire.
/// </summary>
public sealed class @this : System.Collections.Generic.IReadOnlyList<global::app.warning.@this>
{
    private readonly System.Collections.Generic.List<global::app.warning.@this> _items = new();

    public void Add(global::app.warning.@this warning) => _items.Add(warning);
    public void AddRange(System.Collections.Generic.IEnumerable<global::app.warning.@this> warnings)
        => _items.AddRange(warnings);
    public void Clear() => _items.Clear();

    public int Count => _items.Count;
    public global::app.warning.@this this[int index] => _items[index];

    public System.Collections.Generic.IEnumerator<global::app.warning.@this> GetEnumerator()
        => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
