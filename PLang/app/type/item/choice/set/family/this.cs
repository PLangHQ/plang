namespace app.type.item.choice.set.family;

/// <summary>A closed set of a kind family's kinds — an abstract kind whose kinds are its subclasses (<c>hash.kind</c>
/// → sha256, keccak256), or an abstract value whose kinds are its subclasses (<c>size</c> → iec, si: a choice of
/// <c>kind&lt;size&gt;</c>). Its options are the kinds' names, a symbol names a kind (by name or alias), and its name is
/// the type its kinds are kinds of. Its kinds are the ones the app holds, read when it is first asked: a set is named
/// while the types are still being made, before their kinds are held.</summary>
public sealed class @this : set.@this
{
    private readonly System.Lazy<System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this>> _kinds;

    internal @this(System.Type clr, global::app.type.@this owner, global::app.type.list.@this types)
        : base(Option(clr), Family(clr), owner)
        => _kinds = new(() =>
            // template's kinds are held by no type — built from their classes until stage 6 gives template its type
            (typeof(global::app.type.item.template.kind.@this).IsAssignableFrom(clr)
                ? clr.Assembly.GetTypes()
                    .Where(t => clr.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(System.Type.EmptyTypes) != null)
                    .Select(t => (global::app.type.kind.@this)System.Activator.CreateInstance(t)!)
                : types.Items().Select(type => type.kind).OfType<global::app.type.kind.empty.@this>()
                    .SelectMany(root => root.Kinds).Where(Option(clr).IsInstanceOfType))
            .OrderBy(k => k.Name, System.StringComparer.Ordinal).ToList());

    /// <summary>Whether <paramref name="clr"/> is a kind family — an abstract kind its kinds are subclasses of, or a value
    /// whose subclasses are its kinds (<c>[Kinds]</c>; the value itself may be one too: a size of no standard).</summary>
    internal static bool Is(System.Type clr)
        => IsItem(clr) || clr.IsAbstract && typeof(global::app.type.kind.@this).IsAssignableFrom(clr);

    // Whether clr is an item family whose subclasses are its kinds ([Kinds]) — a value, not a kind (a kind is a value too)
    private static bool IsItem(System.Type clr)
        => !typeof(global::app.type.kind.@this).IsAssignableFrom(clr) && typeof(global::app.type.item.@this).IsAssignableFrom(clr)
           && clr.IsDefined(typeof(global::app.Attributes.KindsAttribute), inherit: false);

    // The class an option of the family is: the kind itself, or for an item family the kind of it (kind<size>)
    private static System.Type Option(System.Type clr)
        => IsItem(clr) ? typeof(global::app.type.kind.@this<>).MakeGenericType(clr) : clr;

    // The type a family's kinds are kinds of — the value family itself, or the T its kind is a kind<T> of (kind<size>
    // itself, or an abstract kind under it); template's kinds have no type to be T, and name their facet until stage 6.
    private static string Family(System.Type clr)
        => IsItem(clr) ? global::app.type.item.@this.NameOf(clr)!
            : (clr.IsGenericType ? clr : clr.BaseType) is { IsGenericType: true } parent && parent.GetGenericTypeDefinition() == typeof(global::app.type.kind.@this<>)
            ? global::app.type.item.@this.NameOf(parent.GetGenericArguments()[0])!
            : global::app.type.item.template.kind.@this.Facet;

    protected override System.Collections.Generic.IReadOnlyList<string>? OwnValues => _kinds.Value.Select(k => k.Name).ToList();

    public override object Member(string symbol)
        => _kinds.Value.FirstOrDefault(k => string.Equals(k.Name, symbol, System.StringComparison.OrdinalIgnoreCase)
                                            || k.Alias.Contains(symbol, System.StringComparer.OrdinalIgnoreCase))
           ?? throw new System.ArgumentException($"no {Name} '{symbol}'");
}
