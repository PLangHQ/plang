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

    /// <summary>The value-less citizen — what a failed door answers (the error
    /// rides the asking binding; the value slot stays never-null). The typeless
    /// null; a declared-but-empty slot uses <c>new null.@this(type, kind)</c>.</summary>
    public static @this Absent => global::app.type.@null.@this.Instance;

    /// <summary>
    /// What is in memory NOW — sync, no I/O, no parse, no resolve: the
    /// instance itself. (ToString, Equals, debug views; never a value read.)
    /// </summary>
    public virtual object? Peek() => this;

    /// <summary>
    /// Set a child slot by key — the write counterpart of read-navigation. The
    /// value owns how it stores a child: a <c>dict</c> writes its key, a
    /// <c>list</c> its index. Returns <c>true</c> when this value handled the
    /// write (mutated in place), <c>false</c> when it has no settable child for
    /// <paramref name="key"/> so the caller can fall back. Default: not settable.
    /// (Distinct from <see cref="Write(global::app.channel.serializer.IWriter)"/>,
    /// which serializes the value to a wire writer.)
    /// </summary>
    public virtual bool Write(string key, object? value) => false;

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
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
        => new global::app.type.clr.@this(this, parent.Context).Navigate(parent, key);

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
    public string? Template { get; init; }

    /// <summary>The chain entry whose type name matches — self or a prior.
    /// Null when this value never was that type.</summary>
    public @this? Facet(string typeName)
    {
        for (var i = (this as @this); i != null; i = i._prior)
            if (string.Equals(i.Mint().Name, typeName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    /// <summary>The chain entry that IS a <typeparamref name="T"/> — self or a
    /// prior. The typed face of <see cref="Facet(string)"/>; the default
    /// <see cref="ICreate{TSelf}.Create"/> answers a slot from here (a
    /// <c>Data&lt;file&gt;</c> slot stays satisfied after the file parsed).</summary>
    public T? Facet<T>() where T : @this
    {
        for (var i = (this as @this); i != null; i = i._prior)
            if (i is T t) return t;
        return null;
    }

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
    /// The .NET-edge read over a value-door answer: a typed answer lowers
    /// ITSELF via <see cref="Clr{T}"/>; an answer already in the target CLR
    /// shape passes through (the door still hands raw CLR for rung-2 values
    /// during the consumer-tail transition); anything else is absent. One
    /// owner for the discipline so edge call sites don't each re-implement it.
    /// </summary>
    internal static T? Lower<T>(object? doorAnswer) => doorAnswer switch
    {
        // A plang value lowers to CLR via its own door FIRST — every item is also
        // an `object`, so the universal `T t` arm below would otherwise swallow a
        // `Lower<object>` and hand back the unlowered wrapper instead of its CLR form.
        @this it => it.Clr<T>(),
        T t => t,
        _ => default,
    };

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
        throw new System.InvalidCastException(
            $"{backing.GetType().Name} cannot lower to {target.Name} — the type must own this Clr projection.");
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
            // A raw C# scalar — the WRITER owns rendering each kind (string/bool/number/date/enum/…).
            default: writer.Value(value); break;
        }
    }

}
