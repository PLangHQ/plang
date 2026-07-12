namespace app.type.item.type.list;

/// <summary>
/// An item's type history — the prior VALUES it evolved THROUGH, newest last. A dict parsed from a
/// file holds the file; an image born from a path holds the path. A value that never narrowed has an
/// empty list. The narrowing/constructing type just <c>Add(prior)</c>s — the list owns its add (no
/// accumulate ceremony). Belongs to the item (<c>item.@this.list</c>).
///
/// <para>Holds the prior VALUES (not bare type entities) so slot-satisfaction (<see cref="Facet"/>)
/// reaches the actual file/path — an image bound to a <c>path</c> slot answers with its path. The
/// TYPE view is each entry's <c>.Type</c> (<c>[{image},{path}]</c>). It does NOT reference the owner
/// (the item asks its own type directly) — no back-reference, so a clone never cycles into the owner
/// graph.</para>
/// </summary>
public sealed class @this
{
    private readonly System.Collections.Generic.List<global::app.type.item.@this> _priors = new();

    /// <summary>The values this item evolved through, in order — the tail history (the item's own
    /// type is the head, asked separately by the item itself).</summary>
    public System.Collections.Generic.IReadOnlyList<global::app.type.item.@this> Priors => _priors;

    /// <summary>Record that the item evolved FROM <paramref name="prior"/> — the prior's own history
    /// rides along (a source that was a file, then parsed to a dict). Idempotent by reference.</summary>
    public void Add(global::app.type.item.@this? prior)
    {
        if (prior != null && !_priors.Contains(prior)) _priors.Add(prior);
    }

    /// <summary>Does any prior (recursively) answer to the type <paramref name="other"/>? The tail of
    /// the <c>is</c>-a check — the item asks its own type first, then defers here for the history.</summary>
    public bool Has(global::app.type.@this? other)
    {
        foreach (var p in _priors) if (p.Is(other)) return true;
        return false;
    }

    /// <summary>The first prior (recursively) whose type NAME matches — the facet a <c>%x!file%</c> /
    /// <c>Data&lt;file&gt;</c> slot reaches after a narrow (the item checks itself first).</summary>
    public global::app.type.item.@this? Facet(string typeName)
    {
        foreach (var p in _priors) if (p.Facet(typeName) is { } f) return f;
        return null;
    }

    /// <summary>The first prior (recursively) that IS a <typeparamref name="T"/> (the item checks
    /// itself first).</summary>
    public T? Facet<T>() where T : global::app.type.item.@this
    {
        foreach (var p in _priors) if (p.Facet<T>() is { } f) return f;
        return null;
    }
}
