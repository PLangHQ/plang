namespace app.type.item.text.kind;

/// <summary>
/// The text formats another type holds (json is item's, csv table's, html code's): a text names any of them as its
/// kind — <c>{text, json}</c> is a text whose characters are json — so the kind is coined when a text first names
/// one, one per format and the same object after that, never held ahead. Held on text's type, as a list's element
/// kinds are on list's.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly global::app.type.list.@this _types;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, format.@this?> _coined
        = new(System.StringComparer.OrdinalIgnoreCase);
    // text's name, read off its class once
    private readonly string _owner = global::app.type.item.@this.NameOf(typeof(global::app.type.item.text.@this));

    /// <summary>Coins text's kinds for the formats the types <paramref name="types"/> holds.</summary>
    public @this(global::app.type.list.@this types) : base("*format") => _types = types;

    protected internal override string Owner => _owner;

    /// <summary>The text kind for the format <paramref name="name"/> — when a format of another type answers to it
    /// and its content is characters; null otherwise (a binary format is no kind of text).</summary>
    public override global::app.type.kind.@this? Coin(string name)
        => _coined.GetOrAdd(name, n => _types.Kind(n) is { IsText: true, Owner: not null } held ? new format.@this(held, _owner) : null);
}
