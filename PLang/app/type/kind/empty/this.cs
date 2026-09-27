namespace app.type.kind.empty;

/// <summary>
/// A type's kind when it has none — a bare <c>text</c> has text's empty kind. It knows the type it
/// belongs to and holds that type's kinds (number's precisions, choice's sets, path's schemes,
/// item's json/list/dict/<c>*</c>); its <c>list</c> is them as full types. Its name is empty and it
/// is never written.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly string _owner;
    private readonly System.Collections.Generic.List<global::app.type.kind.@this> _kinds = new();
    private readonly object _gate = new();

    public @this(string owner) : base("") => _owner = owner;

    protected internal override string Owner => _owner;

    /// <summary>The kinds this type holds, in the order they came.</summary>
    internal System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this> kinds
    {
        get { lock (_gate) return _kinds.ToArray(); }
    }

    /// <summary>One of this type's kinds, by its name or an alias; null when it holds none by that name.</summary>
    public global::app.type.kind.@this? this[string name]
        => kinds.FirstOrDefault(k => string.Equals(k.Name, name, System.StringComparison.OrdinalIgnoreCase)
                                     || k.Alias.Contains(name, System.StringComparer.OrdinalIgnoreCase));

    /// <summary>Adds a kind of this type; a kind of the same name replaces the one before it.</summary>
    internal void Add(global::app.type.kind.@this kind)
    {
        lock (_gate)
        {
            _kinds.RemoveAll(k => string.Equals(k.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase));
            _kinds.Add(kind);
        }
    }
}
