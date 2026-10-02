namespace app.type.item.list.kind;

/// <summary>
/// The list's element kinds, held on list's type: a list names any type as its element
/// (<c>list&lt;path&gt;</c>), so the kind for an element is coined when a list first names it — one per element
/// type, the same object after that — rather than held ahead for every type.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly global::app.type.list.@this _types;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, element.@this> _coined
        = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Coins the element kinds of the types <paramref name="types"/> holds.</summary>
    public @this(global::app.type.list.@this types) : base("*element") => _types = types;

    protected internal override string Owner => global::app.type.item.@this.NameOf(typeof(global::app.type.item.list.@this));

    /// <summary>The kind of list whose element is the type <paramref name="name"/>, asked of the type list's own
    /// name door; null when no type has that name.</summary>
    public override global::app.type.kind.@this? Coin(string name)
        => _types.Contains(name)
            ? _coined.GetOrAdd(_types[name].Name, n => new element.@this(_types[n]))
            : null;
}
