namespace app.type.item.choice.set.family;

/// <summary>A closed set of a kind family's kinds — an abstract kind whose kinds are its subclasses (<c>hash.kind</c>
/// → sha256, keccak256). Its options are the kinds' names, a symbol names a kind (by name or alias), and its name is
/// the type its kinds are kinds of. Its kinds are found as the type registry finds every kind
/// (<see cref="global::app.type.kind.@this.Every"/>), built here once: the set has no context to ask the registry for
/// the app's own.</summary>
public sealed class @this : set.@this
{
    private readonly System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this> _kinds;

    internal @this(System.Type clr, System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this> kinds)
        : base(clr, kinds[0].Owner)
        => _kinds = kinds;

    /// <summary>The kinds of the abstract kind <paramref name="clr"/>, by name; none for any other class.</summary>
    internal static System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this> Kinds(System.Type clr)
        => clr.IsAbstract && typeof(global::app.type.kind.@this).IsAssignableFrom(clr)
            ? global::app.type.kind.@this.Every(clr.Assembly).Where(clr.IsInstanceOfType)
                .OrderBy(k => k.Name, System.StringComparer.Ordinal).ToList()
            : [];

    public override System.Collections.Generic.IReadOnlyList<string> Values => _kinds.Select(k => k.Name).ToList();

    public override object Member(string symbol)
        => _kinds.FirstOrDefault(k => string.Equals(k.Name, symbol, System.StringComparison.OrdinalIgnoreCase)
                                      || k.Alias.Contains(symbol, System.StringComparer.OrdinalIgnoreCase))
           ?? throw new System.ArgumentException($"no {Name} '{symbol}'");
}
