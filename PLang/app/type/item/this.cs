using Force.DeepCloner;

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
public abstract class @this : global::app.data.IBooleanResolvable, ICreate<@this>
{
    /// <summary>
    /// The inferred lift — "build WHATEVER this raw is". The apex's OWN ICreate face: for the
    /// UNDECLARED raw (no target type in hand), <c>item</c> is the produced type, so <c>item</c>
    /// owns the construction. Infers the owner via the collection's CONVERSION-ownership door
    /// (<c>App.Type[clrType]</c>) and builds through that entity's own <c>Create</c>; a CLR type no
    /// type owns rides a <c>Clr</c> carrier. Rungs: <c>is item</c> → container narrowing → ownership
    /// lift → enum→choice → <c>Clr</c>. Navigated, not switched. The DECLARED ask is a different door:
    /// <c>App.Type[name].Create(raw, ctx)</c> (the entity door) builds the type the caller named.
    /// ALWAYS returns a value (never null); a bare <c>Data</c> is a producer bug and throws.
    /// </summary>
    public static @this Create(object? raw, global::app.actor.context.@this? context)
    {
        if (raw is null) return global::app.type.item.@null.@this.Instance;
        if (raw is @this already) return already;
        if (raw is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A bare Data may not be stored as a value — return the inner value via its own factory, "
                + "never the Data wrapper.\n" + System.Environment.StackTrace);

        // A sequence of Data / native items narrows to a native list, preserving the instances.
        if (raw is System.Collections.Generic.IEnumerable<global::app.data.@this> dataSeq)
            return new global::app.type.item.list.@this(dataSeq, context!);
        if (raw is System.Collections.Generic.IEnumerable<@this> itemSeq)
            return new global::app.type.item.list.@this(itemSeq, context!);
        if (raw is System.Collections.Generic.List<object?> objList)
            return new global::app.type.item.list.@this(objList, context!);
        if (raw is System.Collections.Generic.Dictionary<string, object?> objDict)
            return new global::app.type.item.dict.@this(objDict, context!);
        // A non-generic container narrows the same way its generic sibling above does — build the
        // native dict/list DIRECTLY from its entries (store raw, type on read), no STJ round-trip.
        if (raw is System.Collections.IDictionary idict)
        {
            var d = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (System.Collections.DictionaryEntry e in idict)
                d[e.Key?.ToString() ?? ""] = e.Value;
            return new global::app.type.item.dict.@this(d, context!);
        }
        if (raw is System.Collections.IList ilist && raw is not byte[])
        {
            var l = new System.Collections.Generic.List<object?>();
            foreach (var e in ilist) l.Add(e);
            return new global::app.type.item.list.@this(l, context!);
        }

        // A CLR enum IS plang's choice (a closed named set) — build choice<T> for the enum. BEFORE
        // the index ask: an enum is neither an item nor _clr-owned, so the door would answer clr.
        if (raw is System.Enum)
            return (@this)System.Activator.CreateInstance(
                typeof(global::app.type.item.choice.@this<>).MakeGenericType(raw.GetType()), raw)!;

        // The natural lift, NAVIGATED off the identity door: the owning entity drives its own cached
        // Create thunk (int → number, string → text). The door is never null — a CLR type no value
        // type owns answers the clr entity, whose Create builds the carrier — so this is the single
        // terminal rung; the old separate Clr fallback dissolves into entity dispatch.
        return context!.App.Type[raw.GetType()].Create(raw, context);
    }

    /// <summary>
    /// THE value door — "I am going to use this value, make yourself ready."
    /// Loads if needed, parses if needed, renders if stamped (ready MEANS
    /// ready: a template whose holes are unfilled is not ready). May answer
    /// AS A DIFFERENT type instance (a <c>file</c> loads its bytes and answers
    /// with the parsed <c>dict</c>, stamping itself as the answer's
    /// <see cref="Prior"/>). The default — a value already in its final form —
    /// answers self with no async hop. The holding <c>Data</c> rebinds to the
    /// answer when <see cref="Cacheable"/> allows; that rebind IS the narrow.
    /// <para><b>Failure:</b> every failure is authored by the type that failed
    /// — the door catches only its OWN known failure modes (file tells IO
    /// stories, source tells parse stories), reports via
    /// <c>data.Fail(error)</c> and answers <see cref="Absent"/>. A truly
    /// unexpected exception is a bug and propagates. The blessed surface of
    /// <paramref name="data"/> here is <c>Fail</c> alone.</para>
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
        => System.Threading.Tasks.ValueTask.FromResult(this);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>
    /// Comparison precedence — higher drives, the lower operand coerces into the higher
    /// via its <c>Create</c>. Used only relationally (who drives/coerces), never a result,
    /// never on the wire. Declared per type on the ×10 table (text 100 … list 750, guid 600,
    /// null 1000); the apex/unranked base is 0. A plugin type declares its own — no central
    /// catalog. Read off the value's real type (both operands are their real shape by the time
    /// <see cref="Compare"/> runs — <see cref="Value"/> parsed any source), so it never
    /// materializes to decide.
    /// </summary>
    public virtual int Rank => 0;

    /// <summary>
    /// Compare this value against <paramref name="other"/> — the reconcile. The higher-ranked
    /// side drives (<see cref="Order"/>); when the right operand drives, its result is
    /// <see cref="global::app.data.ComparisonExtensions.Invert">inverted</see> back to caller
    /// order. Non-virtual — the per-type behavior lives in <see cref="Order"/>; this two-line
    /// reconcile is uniform. Calls <c>other.Order(this)</c>, NEVER <c>other.Compare(this)</c>,
    /// which would re-run the rank pick and recurse.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Compare(@this other)
        => Rank >= other.Rank
            ? await Order(other)
            : global::app.data.ComparisonExtensions.Invert(await other.Order(this));

    /// <summary>
    /// The driver's per-type comparison: coerce <paramref name="other"/> into THIS kind (via the
    /// pure <c>Create</c> core) and order/equate in caller order. The base answers identity —
    /// equal to itself, else not-equal (a value with no order). A non-coercible other is
    /// <see cref="global::app.data.Comparison.Incomparable"/>, not an error. Async so a container
    /// can walk its elements lazily (each element pair awaited as reached, first mismatch exits).
    /// </summary>
    protected virtual System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(@this other)
        => new(ReferenceEquals(this, other) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

    /// <summary>The value-less citizen — what a failed door answers (the error
    /// rides the asking binding; the value slot stays never-null). The typeless
    /// null; a declared-but-empty slot uses <c>new null.@this(type, kind)</c>.</summary>
    public static @this Absent => global::app.type.item.@null.@this.Instance;

    /// <summary>
    /// What is in memory NOW — sync, no I/O, no parse, no resolve: the
    /// instance itself. (ToString, Equals, debug views; never a value read.)
    /// </summary>
    public virtual object? Peek() => this;

    /// <summary>
    /// The child-write door — the write counterpart of <see cref="Get(global::app.data.@this, string)"/>.
    /// The value owns HOW it takes a child: a <c>dict</c> writes its key, a <c>list</c> its index
    /// (<paramref name="isIndex"/> tells positional from named, the grammar's Index-vs-Member fact),
    /// a <c>clr</c> host routes to its kind. Returns the (possibly REPLACED) value — a kind may
    /// materialize an immutable host into a new one; the caller rebinds when the instance differs.
    /// <para>The DEFAULT reflects a public CLR property off THIS value — the write-symmetric twin of
    /// <see cref="Get"/>'s reflect-default (a domain item's <c>step.Action</c>, a scalar's backing).
    /// The member value opens its Data door, lowers ITSELF to the property type (<c>value.Clr</c> — the
    /// sanctioned crossing), and rides into the slot; returns THIS (mutate-in-place). Containers
    /// (<c>dict</c>/<c>list</c>) override with key/index writes; a value with no writable property for
    /// <paramref name="key"/> throws. No context needed — reflection writes the slot directly.</para>
    /// (Distinct from <see cref="Write(global::app.channel.serializer.IWriter)"/>, which serializes.)
    /// </summary>
    public virtual async System.Threading.Tasks.ValueTask<@this> Set(string key, bool isIndex, object? value)
    {
        // A Data opens its door to the concrete value first (a host takes a typed child, never a lazy Data).
        if (value is global::app.data.@this dv) value = await dv.Value();
        var prop = GetType().GetProperty(key, System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null || !prop.CanWrite)
            throw new System.NotSupportedException($"%…% ({Type.Name}) cannot take a child '{key}'");
        if (value is @this iv && !prop.PropertyType.IsInstanceOfType(value))
            value = iv.Clr(prop.PropertyType);
        prop.SetValue(this, value);
        return this;
    }

    /// <summary>
    /// Read counterpart of <see cref="Write(string, object?)"/>: the value
    /// navigates to its own child by <paramref name="key"/>. <c>dict</c> does
    /// key-lookup, <c>list</c> index, the foreign-object carrier reflects its
    /// host — one dispatch, each value owns its body. The default answers
    /// <c>NotFound</c> (IsInitialized=false), so a value with no key-navigation
    /// falls through to the binding's remaining resolution (domain items reflect
    /// themselves via the legacy navigators there; raw stragglers too) until those
    /// collapse onto this method as well.
    /// </summary>
    /// <summary>
    /// The item navigates itself to a child by key. The default reflects a public CLR
    /// property off THIS value — a domain item (<c>step.Index</c>, <c>goal.Steps</c>) or a
    /// scalar leaf's backing (<c>now.Ticks</c> → <c>DateTimeOffset.Ticks</c>). Containers
    /// (<c>dict</c>/<c>list</c>) override with key/index lookup; references
    /// (<c>file</c>/<c>url</c>/<c>source</c>/<c>variable</c>) override to materialise
    /// themselves first. Walks the inheritance chain bottom-up (DeclaredOnly) so a
    /// shadowing property wins without AmbiguousMatch. Returns NotFound when truly absent.
    /// </summary>
    /// <summary>
    /// The item navigates ITSELF to a child by key. The DEFAULT — for a domain item
    /// (goal/step/action/…) — is to reflect its own members through the <c>clr</c> carrier
    /// (CLR reflection lives there; an item is not a CLR object). Types that navigate
    /// differently override: <c>dict</c>/<c>list</c> by key/index (lazy, no sibling
    /// render); a scalar (datetime/number/…) reflects its CLR backing
    /// (<c>clr(Clr&lt;object&gt;())</c>) so <c>%now.Ticks%</c> reaches DateTimeOffset;
    /// a reference/source/computed/variable opens its own door for structure then
    /// navigates the result; <c>null</c>/<c>text</c> can't be walked by key. No
    /// <c>IsLeaf</c> branching, no generic <c>.Value()</c> in the base — the TYPE decides.
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => new global::app.type.clr.@this(this, parent.Context).Get(parent, key);

    /// <summary>
    /// Iteration as <c>(key, value)</c> pairs — the value owns how it iterates,
    /// the courier (<c>Data.EnumerateItems</c>) only delegates here. A leaf is a
    /// single value: <c>foreach</c> over it yields it once (a scalar is itself;
    /// text refuses to iterate as characters). Collections (<c>dict</c>,
    /// <c>list</c>, <c>table</c>) override to yield their elements.
    /// </summary>
    public virtual System.Collections.Generic.IEnumerable<(global::app.data.@this key, global::app.data.@this value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        yield return (new global::app.data.@this("", 0, context: context),
                      new global::app.data.@this("", this, context: context));
    }

    /// <summary>
    /// Whether the holding <c>Data</c> may keep (rebind to) <see cref="Value"/>'s
    /// answer. True when the answer depends on nothing but the value itself
    /// (parse). False when the answer depends on outside state (a template
    /// render, a computed value) — those answer fresh at every use and are
    /// never kept.
    /// </summary>
    public virtual bool Cacheable => true;

    /// <summary>
    /// This value's type history — the values it evolved THROUGH (a dict parsed from a file holds
    /// the file; an image born from a path holds the path). A value that never narrowed has an empty
    /// list. The narrowing/constructing type just <c>list.Add(prior)</c> — the list owns its add
    /// (no accumulate ceremony); <see cref="Is"/> queries it.
    /// </summary>
    private global::app.type.item.type.list.@this? _list;
    internal global::app.type.item.type.list.@this list => _list ??= new();

    /// <summary>
    /// This value's OWN type entity — each type answers ITS way (number stamps its precision as
    /// kind, text its extension, a source its declared judgement, dict/list name themselves). The
    /// single owner of its identity; provenance lives on the value's <see cref="list"/> and is asked
    /// via <see cref="Is"/>. The default derives the name from the namespace tail — a reflection
    /// fallback still used by ~19 domain types (actor/snapshot/…); killing it fully means each
    /// declaring its name (see coder followups).
    /// </summary>
    protected internal virtual global::app.type.@this Type => new(NamespaceTail(GetType()));

    /// <summary>
    /// Is this value (now or in its narrow history) an <paramref name="other"/>? Asks its type
    /// history — a <c>read config.json</c> that narrowed to a <c>dict</c> still answers
    /// <c>Is(file)</c> because the file rides in <see cref="list"/>.
    /// </summary>
    public bool Is(global::app.type.@this? other) => Type.Is(other) || list.Has(other);

    /// <summary>
    /// How this value clones when its holding <c>Data</c> is cloned. The default
    /// is a structural deep copy (so a cloned dict/list/text mutates
    /// independently). A value that holds a <em>live or shared</em> reference —
    /// a host object behind a carrier, a lazy computation, an immutable declared
    /// source, all of which also carry a <c>Context</c> pointing back into the
    /// App graph — overrides this to share itself by reference. Deep-cloning
    /// those would walk the entire runtime (App → CallStack → Context →
    /// Variables → …) and overflow the stack; reference-sharing is both the
    /// correct semantics (a carried host IS shared, per the value model) and the
    /// thing that keeps the clone bounded.
    /// </summary>
    protected internal virtual @this Clone()
        => Force.DeepCloner.DeepClonerExtensions.DeepClone(this);

    /// <summary>
    /// Language tag when this value is a builder-authored template — a value
    /// whose text (or whose nested entries) contain live <c>%ref%</c> holes
    /// that resolve against variables at every USE, never at set. An ordinary
    /// init-only stamp, set at creation by the authored seams (.pr load,
    /// action wire rebuild) — runtime INPUT is never stamped, so a user who
    /// types <c>"%secret%"</c> gets it printed literally. <c>"plang"</c> is
    /// the only language today. Null = not a template; a stamped value is
    /// never cached by the holding Data (see <see cref="Cacheable"/> on the
    /// types that honor the stamp).
    /// </summary>
    // internal set: the build stamps the authored-template flag AFTER the value is built (via
    // Declare), when it detects a %ref%. A value is otherwise immutable; this one build-seam flag
    // is set in place rather than re-minting the whole value.
    public string? Template { get; internal set; }

    /// <summary>
    /// True when the value is already its own final answer — opening its door
    /// (<see cref="Value"/>) returns itself, no resolve/render/load. A literal
    /// scalar is final; a template renders, a file/url loads, a source parses, a
    /// computed answers fresh, so those are NOT final. Metadata/structural reads
    /// may take a final value as-is; a non-final value must be read through its
    /// door, and a carrier must never stand between the Data and it.
    /// </summary>
    internal virtual bool IsFinal => Template == null;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, string> _namespaceTails = new();

    /// <summary>The PLang name of an item CLASS (the namespace-tail rule) —
    /// for messages that must name a type with no instance in hand.</summary>
    internal static string NameOf(System.Type t) => NamespaceTail(t);

    private protected static string NamespaceTail(System.Type t)
        => _namespaceTails.GetOrAdd(t, static ct =>
        {
            var ns = ct.Namespace ?? "item";
            var tail = ns[(ns.LastIndexOf('.') + 1)..];
            return tail.TrimStart('@');
        });

    /// <summary>Build a <typeparamref name="T"/> from a raw structured value through <c>T</c>'s OWN
    /// <see cref="ICreate{T}.Create(object, global::app.data.@this)"/> courier — a domain item building
    /// a nested item (an action its child steps, a step its actions). A shape error lands on
    /// <paramref name="data"/>, never swallowed. This is the generic dispatch the non-generic
    /// <c>T.Create(…)</c> call can't express (that binds the apex lift, not the <c>ICreate</c> member).</summary>
    internal static T? Made<T>(object? raw, global::app.data.@this data)
        where T : @this, ICreate<T>
        => T.Create(raw, data);

    /// <summary>A human-readable name for a type in an error — a plang <c>@this</c> type reads as its
    /// last one/two namespace segments (<c>parameter.list</c>, <c>action</c>, <c>item</c>), a CLR type
    /// drops the generic-arity backtick and spells its element (<c>List&lt;Data&gt;</c>). Diagnostics
    /// only; never a stable identity.</summary>
    private protected static string Readable(System.Type t)
    {
        if (t.Name.TrimStart('@') == "this")
        {
            var parts = (t.Namespace ?? "item").Split('.');
            return parts.Length >= 2 && parts[^2] is not ("item" or "type")
                ? $"{parts[^2]}.{parts[^1]}".TrimStart('@')
                : parts[^1].TrimStart('@');
        }
        var name = t.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];
        if (t.IsGenericType)
            name += "<" + string.Join(",", System.Linq.Enumerable.Select(t.GetGenericArguments(), Readable)) + ">";
        return name;
    }

    /// <summary>
    /// Membership — each type owns its own answer: text by substring (ordinal,
    /// case-insensitive), list by element equality through THE comparison
    /// entry, dict by key, directory by its listing. The default is false — a
    /// scalar contains nothing; there is no ToString fallback (a needle never
    /// matches a serialization).
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => System.Threading.Tasks.ValueTask.FromResult(false);

    /// <summary>
    /// Emptiness — each type owns its own answer: text → whitespace-only,
    /// dict/list → no entries, null/absent → empty. Async because a reference
    /// may load to answer (same precedent as <see cref="AsBooleanAsync"/>).
    /// The default is false — a present value with no emptier notion is not
    /// empty.
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(false);

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
    /// (<see cref="ClrConvert"/>); the type's own loss policy applies before the
    /// hand-over (number's tower). Lossy conversion THROWS — never a silent
    /// default. The base default hands <see cref="Peek"/> (the in-memory value):
    /// a type whose Peek IS itself (a domain value — path/image/code) lowers to
    /// itself for an object target and lets ClrConvert error honestly on a real
    /// concrete mismatch; leaves/containers (text/number/dict/list) override with
    /// their real backing. Internal — engine plumbing for the typed boundary
    /// (<c>Data.Clr&lt;T&gt;</c>, hydration); the plang-facing surface stays typed.
    /// </summary>
    internal virtual object? Clr(System.Type target) => ClrConvert(Peek(), target);

    /// <summary>Generic sugar over <see cref="Clr(System.Type)"/> — the
    /// compile-time-known-target form.</summary>
    internal T? Clr<T>() => (T?)Clr(typeof(T));

    /// <summary>
    /// The typed source-face seam for CLR-facing machinery (ctor matching,
    /// kind probes, TryConvert): a LEAF value lowers to its own backing via
    /// its <see cref="Clr(System.Type)"/>; containers and non-items pass
    /// through. The single owner of the old per-site
    /// "<c>is item { IsLeaf: true } ? Clr&lt;object&gt;() : v</c>" transform.
    /// </summary>
    internal static object? Backing(object? v)
        => v is @this { IsLeaf: true } l ? l.Clr<object>() : v;


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

        // Terminal — LOWER never re-enters the conversion hub. A value the identity /
        // ChangeType arms can't carry to the target must own that projection in its OWN
        // Clr (dict→record, list→collection, choice→enum). Reaching here means the caller
        // asked Clr to do a CONVERT (raw→plang, cross-family) — that's the type system's
        // job (type.Create / the target family), not the lower door's.
        var from = backing is @this bi ? $"{Readable(backing.GetType())}(plang {bi.Type?.Name ?? "?"})" : Readable(backing.GetType());
        throw new System.InvalidCastException(
            $"cannot lower a {from} into {Readable(target)}: the target owns no Clr projection for this shape. " +
            $"A cross-shape convert (list→{Readable(target)}, dict→record, raw→plang) belongs on the TARGET's own Clr or its type family, not this lower door.");
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
    /// Applies a declared <paramref name="kind"/> to a kindless leaf, answering the
    /// type's own way (text re-mints with the kind, binary rebuilds). The default is a
    /// no-op — a value whose kind doesn't apply (or that already carries one) returns
    /// self. Owns the "how do I take a kind" knowledge on the leaf, not in a caller switch.
    /// </summary>
    public virtual @this Kinded(string? kind) => this;

    /// <summary>
    /// The raw string form this leaf carries, if any (text's characters, a source's
    /// undecoded raw) — used to spot a Variable name or an unrendered %ref% template.
    /// Default null: the leaf has no raw string face.
    /// </summary>
    public virtual string? RawText => null;

    /// <summary>
    /// The raw byte form this value carries (binary's bytes, an image's loaded bytes,
    /// text's UTF-8 content), or null when the value has no byte face — a container
    /// serializes through a format instead. Mirror of <see cref="RawText"/>.
    /// </summary>
    public virtual byte[]? RawBytes => null;

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
    /// True when this value is a REFERENCE to a named binding (a bare name-slot
    /// <c>variable</c>, a full-match <c>%ref%</c> source, or a full-match template
    /// <c>text</c>) rather than content. The instance understands what it is; content
    /// values answer false. A reference resolves to its binding via <see cref="Get"/>.
    /// </summary>
    public virtual bool IsVariable => false;

    /// <summary>
    /// The Data instance this reference is bound to — the lazy name-hop: it Gets its own
    /// name against the variable store (<paramref name="ctx"/>) WITHOUT opening the target's
    /// value door, so a pending read stays unread. A content value is not a reference and
    /// answers null (only ever called when <see cref="IsVariable"/>). Each reference carrier
    /// overrides this with its own name — the name never leaves the instance.
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => new((global::app.data.@this?)null);

    /// <summary>
    /// True only for the null value (the <c>null</c> citizen). Every other value is
    /// present, so the default is false. Consumers test a value's nullness with
    /// <c>value.IsNull</c> instead of a C# <c>!= null</c> reference check — a typed
    /// null is a real instance, so <c>!= null</c> wrongly reads it as present.
    /// </summary>
    public virtual bool IsNull => false;

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
    /// The item writes ITSELF to the wire — one async pass that merges flatten
    /// (the old Normalize) and render (Write), resolving lazily as it reaches each
    /// node. The default is the leaf path: emit my bare wire form via <see cref="Write"/>
    /// (a non-leaf with no override hits Write's loud throw). Containers and references
    /// override: dict/list walk + await children, variable resolves itself, clr reflects
    /// its host. No intermediate tree, no pre-resolve walk; <c>await</c>s happen here,
    /// between the writer's synchronous buffer writes.
    /// </summary>
    public virtual System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>
    /// Reflective Output for a STRUCTURAL item — writes its tagged property bag, the View selecting
    /// the attribute set (<see cref="global::app.channel.serializer.filter.Tagged"/>:
    /// <c>Out→[Out]</c>, <c>Store→[Store]</c>, <c>Debug→all</c>). Each property value writes ITSELF:
    /// an <see cref="@this"/> via its own <see cref="Output"/>, a raw C# scalar via the writer, a
    /// sequence as an array. The general-object wire form; leaves and special shapes
    /// (dict/list/clr/…) override <see cref="Output"/> directly. Replaces <c>NormalizeObject</c>.
    /// </summary>
    [System.Obsolete("Superseded by the reflection (*) kind's Output — do not add new callers.")]
    protected async System.Threading.Tasks.ValueTask OutputTagged(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(GetType(), mode))
        {
            if (entry.Masked) { writer.Name(entry.WireName); writer.String("****"); continue; }
            var value = entry.Property.GetValue(this);
            if (value == null) continue;   // nulls omitted (WhenWritingNull)
            writer.Name(entry.WireName);
            await WriteReflected(writer, value, mode, context);
        }
        writer.EndObject();
    }

    // Writes a value reflected off a property: a plang value writes itself (Output), a sequence
    // becomes an array of self-writes, a raw C# scalar goes through the writer. This is the
    // reflection→wire boundary — C# primitives can't write themselves, so the writer renders them.
    private static async System.Threading.Tasks.ValueTask WriteReflected(
        global::app.channel.serializer.IWriter writer, object value, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        switch (value)
        {
            // A plang value writes ITSELF.
            case @this item: await item.Output(writer, mode, context); break;
            // A Data-typed property (action.parameters) self-writes too — async, so it routes here,
            // NOT through writer.Value (sync, peels to the OLD Peek().Write path).
            case global::app.data.@this d: await d.Output(writer, mode, context); break;
            // BRIDGE: a raw C# collection (goal.steps / action.modifiers / action.parameters, until
            // those become item.@this lists) writes as an array of self-writes. Deleted once they are items.
            case System.Collections.IEnumerable seq when value is not (string or byte[]):
                writer.BeginArray(-1);
                foreach (var el in seq) if (el != null) await WriteReflected(writer, el, mode, context);
                writer.EndArray();
                break;
            // A domain HOST riding an [Out] property (test.Goal) — not writer vocabulary. This is the
            // reflection→wire boundary, so it lifts the host through the door; clr(host).Output renders
            // its own tagged face ([Out]/[Sensitive] discipline intact). Writer-vocabulary scalars
            // (string/int/date/…) keep the writer's rendering.
            default:
                if (context != null && value is not System.IConvertible
                    && context.App.Type[value.GetType()].ClrType == typeof(global::app.type.clr.@this))
                {
                    await global::app.type.item.@this.Create(value, context).Output(writer, mode, context);
                    break;
                }
                writer.Value(value); break;
        }
    }

}
