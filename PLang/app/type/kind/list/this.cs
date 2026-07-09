using System.Reflection;

namespace app.type.kind.list;

/// <summary>
/// The kinds — the collection of every <see cref="app.type.kind.@this"/> (json, list, dict,
/// <c>*</c>, later yaml/xml), reached as <c>app.type.Kind[name|clrType]</c>. Owns SELECTION +
/// LIFECYCLE: a value asks for its kind by name (a wire descriptor <c>kind:"json"</c>) or by
/// its host's CLR type (a <c>clr</c> at birth). Per-App, born with the App's context, so the
/// kinds it mints are stamped (their <c>Type</c> resolution reaches the App's readers).
///
/// <para>Never a static factory (<c>kind.Of</c>) and never an implicit <c>(kind)"json"</c> — one
/// door, reached by navigation. The indexer never returns null: an unknown NAME mints a base
/// instance carrying the name (its verb defaults are its behavior); an unclaimed CLR type is the
/// <c>*</c> reflection kind (the catch-all for any object).</para>
/// </summary>
public sealed class @this
{
    // Kind logic is app-independent, so the type discovery runs ONCE per process.
    private static readonly Discovered _shared = new(typeof(@this).Assembly);

    private readonly global::app.actor.context.@this? _context;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.type.kind.@this> _byName
        = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, global::app.type.kind.@this> _byClr
        = new();

    public @this(global::app.actor.context.@this? context) => _context = context;

    /// <summary>The kind for a name — a known name → its subclass; an unknown name → a base
    /// instance carrying the name (the defaults are its behavior). Never null.</summary>
    public global::app.type.kind.@this this[string name]
        => _byName.GetOrAdd(name, n => _shared[n] is { } t
            ? Mint(t)
            : new global::app.type.kind.@this(n, _context));

    /// <summary>The kind a host object of <paramref name="clrType"/> is — a claimed CLR form
    /// (exact wins, then assignable: <c>JsonElement</c>→json, <c>IList</c>→list, <c>IDictionary</c>
    /// →dict), else the <c>*</c> reflection kind (any other object). Never null.</summary>
    public global::app.type.kind.@this this[System.Type clrType]
        => _byClr.GetOrAdd(clrType, ct => Mint(_shared[ct] ?? _shared.ReflectionType));

    private global::app.type.kind.@this Mint(System.Type kindType)
        => (global::app.type.kind.@this)System.Activator.CreateInstance(kindType, new object?[] { _context })!;

    // A scanned set of kind CLR types + their name / CLR-form claims. Immutable once built.
    private sealed class Discovered
    {
        private readonly System.Collections.Generic.Dictionary<string, System.Type> _byName
            = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.List<(System.Type ClrForm, System.Type KindType)> _byClrForm = new();
        public System.Type ReflectionType { get; } = typeof(global::app.type.item.kind.reflection.@this);

        public Discovered(Assembly assembly)
        {
            foreach (var t in assembly.GetTypes())
                if (typeof(global::app.type.kind.@this).IsAssignableFrom(t)
                    && t is { IsAbstract: false } && t != typeof(global::app.type.kind.@this))
                {
                    var probe = (global::app.type.kind.@this)System.Activator.CreateInstance(t, new object?[] { null })!;
                    _byName[probe.Name] = t;
                    if (probe.ClrForm is { } cf) _byClrForm.Add((cf, t));
                }
        }

        // The kind class claiming a name — a keyed lookup, so an indexer, not a verb+noun method.
        public System.Type? this[string name] => _byName.TryGetValue(name, out var t) ? t : null;

        // The kind class claiming a CLR type — exact ClrForm, then the most-derived assignable.
        public System.Type? this[System.Type clr]
        {
            get
            {
                if (clr == typeof(string)) return null;                                 // string is a scalar, never a sequence kind
                foreach (var (cf, kt) in _byClrForm) if (cf == clr) return kt;          // exact wins
                // Then the MOST-DERIVED assignable claim (IDictionary → dict beats IEnumerable →
                // list for a Dictionary; a claim `cf` beats the best when best is assignable FROM it).
                System.Type? bestCf = null, bestKt = null;
                foreach (var (cf, kt) in _byClrForm)
                    if (cf.IsAssignableFrom(clr) && (bestCf is null || bestCf.IsAssignableFrom(cf)))
                        (bestCf, bestKt) = (cf, kt);
                return bestKt;
            }
        }
    }
}
