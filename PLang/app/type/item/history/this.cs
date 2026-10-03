namespace app.type.item.history;

/// <summary>
/// An item's history — the values it evolved THROUGH, newest last. A dict parsed from a file holds
/// the file; an image born from a path holds the path. A value that never narrowed has an empty
/// history. The narrowing/constructing type just <c>Add(prior)</c>s — the history owns its add.
/// Belongs to the item (<c>item.@this.history</c>).
///
/// <para>Holds the prior VALUES (not bare type entities) so slot-satisfaction reaches the actual
/// file/path — an image bound to a <c>path</c> slot answers with its path. The TYPE view is each
/// entry's <c>.Type</c> (<c>[{image},{path}]</c>). It does NOT reference the owner (the item asks its
/// own type directly) — no back-reference, so a clone never cycles into the owner graph.</para>
/// </summary>
public sealed class @this
{
    private readonly System.Collections.Generic.List<global::app.type.item.@this> _list = new();

    /// <summary>The values this item evolved through, in order — the tail of its history (the item's
    /// own type is the head, asked separately by the item itself).</summary>
    public System.Collections.Generic.IReadOnlyList<global::app.type.item.@this> list => _list;

    /// <summary>Record that the item evolved FROM <paramref name="prior"/> — the prior's own history
    /// rides along (a source that was a file, then parsed to a dict). Idempotent by reference.</summary>
    public void Add(global::app.type.item.@this? prior)
    {
        if (prior != null && !_list.Contains(prior)) _list.Add(prior);
    }

    /// <summary>Does any prior (recursively) answer to the type <paramref name="other"/>? The tail of
    /// the <c>is</c>-a check — the item asks its own type first, then defers here for the history.</summary>
    public bool Has(global::app.type.@this? other)
    {
        foreach (var p in _list) if (p.Is(other)) return true;
        return false;
    }

    /// <summary>Was any prior (recursively) of the kind <paramref name="name"/>? A read <c>config.json</c> parsed to a
    /// dict still answers <c>is json</c>.</summary>
    public bool HasKind(string name)
    {
        foreach (var p in _list) if (p.IsKind(name)) return true;
        return false;
    }
}
