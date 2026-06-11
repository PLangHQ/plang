namespace app.type.item;

/// <summary>
/// The apex of the PLang value lattice (≈ C# <c>object</c>) <em>and</em> the
/// un-narrowed type tag a value carries before it is examined. Every value
/// wrapper — <c>number</c>, <c>text</c>, <c>dict</c>, <c>list</c>, <c>bool</c>,
/// <c>null</c>, the date-family, <c>duration</c>, <c>path</c>/<c>image</c>/<c>code</c>,
/// <c>Variable</c>, <c>Ask</c> — is a <c>: item.@this</c>, so a <c>Data&lt;T&gt;</c>
/// slot can constrain <c>where T : item</c> and a raw CLR value can never ride a
/// value slot.
///
/// <para><b>Storage-free.</b> Stamping a type does not parse: an un-narrowed
/// value's serialized form rides on <c>Data</c> (lazy materialization,
/// <c>type-system.md</c>), not on this base. So <c>item</c> owns no value slot —
/// the backing (<c>int</c>/<c>string</c>/<c>List&lt;Data&gt;</c>) stays on each
/// subtype. <c>item</c> carries only <em>behavior</em>, never a blob field, so a
/// narrowed subtype (<c>number</c>/<c>dict</c>/<c>Ask</c>) inherits no dead
/// un-narrowed state (OBP smell #6).</para>
///
/// <para><b>The universal contract is truthiness + the lazy narrow only.</b>
/// Ordering (<see cref="global::app.data.IOrderableValue"/>) and value-equality
/// (<see cref="global::app.data.IEquatableValue"/>) stay opt-in interfaces — a
/// value implements them only when it honors them (<c>list</c> orders, <c>dict</c>
/// does not). <c>item</c> must <b>not</b> implement either, or <c>dict : item</c>
/// would inherit an order it can't honor (its <c>Compare.Order</c> throws).</para>
/// </summary>
public abstract class @this : global::app.data.IBooleanResolvable
{
    /// <summary>
    /// The value door on the instance itself — "I am going to use this value,
    /// give it to me ready." Loads and parses if needed, and may answer AS A
    /// DIFFERENT type instance (a <c>file</c> loads its bytes and answers with
    /// the parsed <c>dict</c>, stamping itself as the answer's <see cref="Prior"/>).
    /// The default — a value already in its final form — answers self with no
    /// async hop. The holding <c>Data</c> rebinds to the answer when
    /// <see cref="Cacheable"/> allows; that rebind IS the narrow.
    /// (This is the type-side <c>Value()</c> door of the value model; named
    /// <c>Ready</c> because several types still expose a public <c>Value</c>
    /// property, which C# cannot coexist with an override of the same name.)
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<@this> Ready()
        => System.Threading.Tasks.ValueTask.FromResult(this);

    /// <summary>
    /// What is in memory NOW — sync, no I/O, no parse, no resolve. The default
    /// is the instance itself; a loading type whose bytes are in memory but
    /// unparsed answers with those bytes; the rung-2 carrier answers with the
    /// CLR object it carries.
    /// </summary>
    public virtual object? Peek() => this;

    /// <summary>
    /// What the value door hands over after <see cref="Ready"/> answered —
    /// the transitional consumer-facing form (tightens to the instance itself
    /// when the consumer tail converts). Defaults to <see cref="Peek"/>; a
    /// source whose bytes could not parse hands the raw bytes (Peek's utf-8
    /// face is a look, not the value).
    /// </summary>
    internal virtual object? Open() => Peek();

    /// <summary>
    /// Whether the holding <c>Data</c> may keep (rebind to) <see cref="Ready"/>'s
    /// answer. True when the answer depends on nothing but the value itself
    /// (parse). False when the answer depends on outside state (a template
    /// render, a computed value) — those answer fresh at every use and are
    /// never kept.
    /// </summary>
    public virtual bool Cacheable => true;

    /// <summary>
    /// The narrow chain — the instance this value evolved FROM (a dict parsed
    /// from a file holds the file here). Stamped once by the narrowing type at
    /// mint; null for a value that never narrowed. Newest first: walking
    /// <c>Prior</c> links yields the full history.
    /// </summary>
    public @this? Prior => _prior;
    private @this? _prior;

    /// <summary>
    /// Joins <paramref name="prior"/> into this value's creation history —
    /// called by the type that minted this answer (the file accumulates itself
    /// onto the dict it parsed). Appends at the END of the chain — a parse
    /// answer may already carry its source form as a prior (dict ← source ←
    /// file). Never rewrites an existing link.
    /// </summary>
    internal void Accumulate(@this prior)
    {
        if (ReferenceEquals(prior, this)) return;
        var tail = (this as @this)!;
        while (tail._prior != null)
        {
            if (ReferenceEquals(tail._prior, prior)) return;
            tail = tail._prior;
        }
        if (!ReferenceEquals(tail, prior)) tail._prior = prior;
    }

    /// <summary>
    /// This value's type — the entity, minted on ask, the whole chain riding
    /// along (a dict parsed from a file answers <c>[dict, file]</c>). The
    /// instance is the single owner of its identity; <c>Data.Type</c> is a
    /// pure forward to this. Internal: a fresh entity per get — a public
    /// property here would send reflection walks (serializers, structural
    /// comparers) into an unbounded mint chain.
    /// </summary>
    internal global::app.type.@this Type
    {
        get
        {
            var minted = Mint();
            for (var p = _prior; p != null; p = p._prior)
                minted.Accumulate(p.Mint());
            return minted;
        }
    }

    /// <summary>
    /// Mints this value's own type entity — each type answers ITS way (number
    /// stamps its precision as kind, text its extension, a source its declared
    /// judgement). The default derives the name from the class's namespace
    /// tail (<c>app.type.file.@this</c> → <c>file</c>; the repo convention that
    /// a type's folder IS its name) and the CLR mate from the value's backing.
    /// </summary>
    protected internal virtual global::app.type.@this Mint()
        // No CLR mate stamped here — a primitive name resolves its mate through
        // the alias table in the entity's own ctor; a domain name resolves
        // through the registry when a Context is present. Types whose mate is
        // value-derived (number's tower) override.
        => new(NamespaceTail(GetType()));

    /// <summary>The chain entry whose type name matches — self or a prior.
    /// Null when this value never was that type.</summary>
    public @this? Facet(string typeName)
    {
        for (var i = (this as @this); i != null; i = i._prior)
            if (string.Equals(i.Mint().Name, typeName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    /// <summary>
    /// True when <paramref name="value"/>'s type overrides <see cref="Ready"/> —
    /// its own door does real work (file/url load, source parses, computed
    /// answers fresh). Metadata reads must not open such a door, and a carrier
    /// must never stand between the Data and it.
    /// </summary>
    internal static bool OwnsDoor(@this value)
        => _doorOwners.GetOrAdd(value.GetType(), static t =>
            t.GetMethod(nameof(Ready))!.DeclaringType != typeof(@this));

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, bool> _doorOwners = new();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, string> _namespaceTails = new();

    private protected static string NamespaceTail(System.Type t)
        => _namespaceTails.GetOrAdd(t, static ct =>
        {
            var ns = ct.Namespace ?? "item";
            var tail = ns[(ns.LastIndexOf('.') + 1)..];
            return tail.TrimStart('@');
        });

    /// <summary>
    /// Synchronous truthiness — the hot path so a plain <c>if %bool%</c> never
    /// takes an async hop. The default is "reference-ish item is truthy when
    /// present"; concrete types override (empty text / zero / empty collection /
    /// null are falsy). I/O truthiness (<c>path</c> existence) overrides
    /// <see cref="AsBooleanAsync"/> instead.
    /// </summary>
    public virtual bool IsTruthy() => true;

    /// <summary>
    /// THE CLR EXIT DOOR — converts this value to the raw CLR
    /// <paramref name="target"/>. The pattern is uniform across types: the
    /// object hands ITS OWN backing to the shared mechanical converter
    /// (<see cref="ClrConvert"/>) in a one-line override; the type's own loss
    /// policy applies before the hand-over (number's tower). Lossy conversion
    /// THROWS — never a silent default. The base THROWS too: a type that
    /// declared no backing surfaces loudly the first time someone needs its
    /// CLR form, instead of guessing through a string. Internal — engine
    /// plumbing for the typed boundary (<c>Data.Clr&lt;T&gt;</c>, hydration);
    /// the plang-facing surface stays fully typed.
    /// </summary>
    internal virtual object? Clr(System.Type target)
        => throw new System.NotSupportedException(
            $"'{GetType().FullName}' declares no CLR backing — override Clr(Type) with the type's own backing to convert to {target.Name}.");

    /// <summary>Generic sugar over <see cref="Clr(System.Type)"/> — the
    /// compile-time-known-target form.</summary>
    internal T? Clr<T>() => (T?)Clr(typeof(T));

    /// <summary>
    /// The .NET-edge read over a value-door answer: a typed answer lowers
    /// ITSELF via <see cref="Clr{T}"/>; an answer already in the target CLR
    /// shape passes through (the door still hands raw CLR for rung-2 values
    /// during the consumer-tail transition); anything else is absent. One
    /// owner for the discipline so edge call sites don't each re-implement it.
    /// </summary>
    internal static T? Lower<T>(object? doorAnswer) => doorAnswer switch
    {
        T t => t,
        @this it => it.Clr<T>(),
        _ => default,
    };

    /// <summary>
    /// The shared mechanics under every <see cref="Clr(System.Type)"/>: the
    /// value the TYPE handed over (its own backing — ownership stays with the
    /// type) converted via the engine's one converter. Identity short-circuits;
    /// failure throws loudly with the converter's bind message.
    /// </summary>
    private protected static object? ClrConvert(object? backing, System.Type target)
    {
        if (backing == null) return null;
        if (target.IsInstanceOfType(backing)) return backing;

        // Primitive targets convert directly (invariant, throws on junk/overflow)
        // — BEFORE the engine converter, whose family-hook arm would answer a
        // numeric target with the born-native WRAPPER (its job is the opposite
        // direction: CLR → plang).
        if (backing is System.IConvertible && typeof(System.IConvertible).IsAssignableFrom(target)
            && target != typeof(object) && !target.IsEnum)
            return System.Convert.ChangeType(backing, target, System.Globalization.CultureInfo.InvariantCulture);

        var (converted, error) = global::app.type.catalog.@this.TryConvert(backing, target);
        if (error != null)
            throw new System.InvalidCastException(error.Message);
        // The converter answered with a born-native wrapper (its family-hook
        // arm) — ask the WRAPPER for the raw target; its backing terminates the
        // recursion at the identity arm.
        if (converted is @this wrapper && !target.IsInstanceOfType(converted))
            return wrapper.Clr(target);
        return converted;
    }

    /// <summary>
    /// <see cref="global::app.data.IBooleanResolvable"/> — defaults to the sync
    /// <see cref="IsTruthy"/>. Only a value whose truthiness needs I/O
    /// (<c>path</c>) overrides this.
    /// </summary>
    public virtual System.Threading.Tasks.Task<bool> AsBooleanAsync()
        => System.Threading.Tasks.Task.FromResult(IsTruthy());

    /// <summary>
    /// The lazy narrow: an un-narrowed value (<c>type=item, kind=json</c>) reads
    /// its raw form at first touch and re-stamps itself <c>dict</c>/<c>list</c>.
    /// The default — for an already-narrowed subtype — is a no-op that returns
    /// <c>self</c>, so a typed value carries no un-narrowed state.
    /// </summary>
    public virtual @this Narrow() => this;

    /// <summary>
    /// True for a scalar leaf — a value with no sub-structure that rides the wire as
    /// a single bare token (text/number/bool/date-family/duration/binary/null/choice).
    /// Normalize passes a leaf straight to the writer (which renders it bare); a
    /// non-leaf (<c>dict</c>/<c>list</c> with children, <c>path</c>/<c>image</c>/<c>code</c>
    /// and other domain values that reflect) takes its own normalization branch. A new
    /// scalar type opts in here instead of being listed inside Normalize.
    /// </summary>
    public virtual bool IsLeaf => false;

    /// <summary>
    /// Render this value's bare wire form into the format-neutral
    /// <see cref="global::app.channel.serializer.IWriter"/> — the leaf-serializer
    /// behavior (OBP Rule 9: the value owns its wire shape, the writer never
    /// type-switches). Only leaves are asked (Normalize routes non-leaves through
    /// their own branches); the default throws so a missing override is loud.
    /// </summary>
    public virtual void Write(global::app.channel.serializer.IWriter writer)
        => throw new System.NotSupportedException(
            $"{GetType().Name} has no bare wire form — it is not a leaf value.");

    /// <summary>
    /// The raw CLR projection of this value — the single unwrap at the
    /// typed-conversion leaf (<c>→ returns string/int/DateTime/…</c>). A scalar
    /// returns its backing CLR scalar (<c>text → string</c>, <c>number → the
    /// boxed numeric</c>, <c>bool → bool</c>, the date-family → their CLR struct);
    /// <c>dict</c>/<c>list</c> decompose to a raw <c>Dictionary</c>/<c>List</c>;
    /// <c>null</c> → C# <c>null</c>. The default — a domain value that <em>is</em>
    /// its own raw form (<c>path</c>/<c>image</c>/<c>code</c>) — returns
    /// <c>self</c>. This replaces the per-type unwrap switch: the value owns its
    /// own raw projection, so the conversion leaf asks once with no type-switch.
    /// </summary>
    public virtual object? ToRaw() => this;
}
