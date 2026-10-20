namespace app.type.item.choice.set.family;

/// <summary>A closed set of a kind family's kinds — an abstract kind whose kinds are its subclasses (<c>hash.kind</c>
/// → sha256, keccak256). Its options are the kinds' names, a symbol names a kind (by name or alias), and its name is
/// the type its kinds are kinds of. Its kinds are the ones the app holds, read when it is first asked: a set is named
/// while the types are still being made, before their kinds are held.</summary>
public sealed class @this : set.@this
{
    private readonly System.Lazy<System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this>> _kinds;

    internal @this(System.Type clr, global::app.type.@this owner, global::app.type.list.@this types)
        : base(clr, Family(clr), owner)
        => _kinds = new(() =>
            // template's kinds are held by no type — built from their classes until stage 6 gives template its type
            (typeof(global::app.type.item.template.kind.@this).IsAssignableFrom(clr)
                ? clr.Assembly.GetTypes()
                    .Where(t => clr.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(System.Type.EmptyTypes) != null)
                    .Select(t => (global::app.type.kind.@this)System.Activator.CreateInstance(t)!)
                : (types[Family(clr)].kind as global::app.type.kind.empty.@this)?.Kinds.Where(clr.IsInstanceOfType) ?? [])
            .OrderBy(k => k.Name, System.StringComparer.Ordinal).ToList());

    /// <summary>Whether <paramref name="clr"/> is a kind family — an abstract kind its kinds are subclasses of.</summary>
    internal static bool Is(System.Type clr)
        => clr.IsAbstract && typeof(global::app.type.kind.@this).IsAssignableFrom(clr);

    // The type a family's kinds are kinds of — the T its abstract kind is a kind<T> of; template's kinds have no type
    // to be T, and name their facet until stage 6.
    internal static string Family(System.Type clr)
        => clr.BaseType is { IsGenericType: true } parent && parent.GetGenericTypeDefinition() == typeof(global::app.type.kind.@this<>)
            ? global::app.type.item.@this.NameOf(parent.GetGenericArguments()[0])!
            : global::app.type.item.template.kind.@this.Facet;

    protected override System.Collections.Generic.IReadOnlyList<string>? OwnValues => _kinds.Value.Select(k => k.Name).ToList();

    public override object Member(string symbol)
        => _kinds.Value.FirstOrDefault(k => string.Equals(k.Name, symbol, System.StringComparison.OrdinalIgnoreCase)
                                            || k.Alias.Contains(symbol, System.StringComparer.OrdinalIgnoreCase))
           ?? throw new System.ArgumentException($"no {Name} '{symbol}'");
}
