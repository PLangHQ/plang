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
/// <para><b>Ordering and equality belong to the value.</b> Every value compares through
/// <see cref="Compare"/>, which hands the comparison to the higher-ranked side's
/// <see cref="Order"/>. A value with an order answers less/greater; one without
/// (<c>bool</c>, <c>dict</c>, <c>null</c>) answers only equal or not equal; the base
/// answers identity.</para>
/// </summary>
[global::app.Attributes.PlangType("item")]
public abstract class @this : global::app.data.IBooleanResolvable, ICreate<@this>
{
    /// <summary>
    /// The inferred lift — "build WHATEVER this raw is". The apex's OWN ICreate face: for the
    /// UNDECLARED raw (no target type in hand), <c>item</c> is the produced type, so <c>item</c>
    /// owns the construction. Infers the owner via the collection's CONVERSION-ownership door
    /// (<c>App.type.list[clrType]</c>) and builds through that entity's own <c>Create</c>; a CLR type no
    /// type owns rides a <c>Clr</c> carrier. Rungs: <c>is item</c> → container narrowing → ownership
    /// lift → enum→choice → <c>Clr</c>. Navigated, not switched. The DECLARED ask is a different door:
    /// <c>App.type.list[name].Create(raw, ctx)</c> (the entity door) builds the type the caller named.
    /// ALWAYS returns a value (never null); a bare <c>Data</c> is a producer bug and throws.
    /// </summary>
    public static @this Create(object? raw, global::app.actor.context.@this context)
    {
        if (raw is null) return context.App.type.list["null"].Empty(context);
        if (raw is @this already) return already;
        if (raw is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A bare Data may not be stored as a value — return the inner value via its own factory, "
                + "never the Data wrapper.\n" + System.Environment.StackTrace);

        // A sequence of Data / native items narrows to a native list, preserving the instances.
        if (raw is System.Collections.Generic.IEnumerable<global::app.data.@this> dataSeq)
            return new global::app.type.item.list.@this(dataSeq, context.App.type.list["list"]);
        if (raw is System.Collections.Generic.IEnumerable<@this> itemSeq)
            return new global::app.type.item.list.@this(itemSeq, context.App.type.list["list"]);
        if (raw is System.Collections.Generic.List<object?> objList)
            return new global::app.type.item.list.@this(objList, context.App.type.list["list"]);
        if (raw is System.Collections.Generic.Dictionary<string, object?> objDict)
            return new global::app.type.item.dict.@this(objDict, context.App.type.list["dict"]);
        // A non-generic container narrows the same way its generic sibling above does — build the
        // native dict/list DIRECTLY from its entries (store raw, type on read), no STJ round-trip.
        if (raw is System.Collections.IDictionary idict)
        {
            var d = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (System.Collections.DictionaryEntry e in idict)
                d[e.Key?.ToString() ?? ""] = e.Value;
            return new global::app.type.item.dict.@this(d, context.App.type.list["dict"]);
        }
        // Any remaining enumerable — an IList, an IReadOnlyList<T> (a domain X.list like warning.list),
        // an array, a HashSet<T> — narrows to a native plang list (store raw, type on read). string is
        // IEnumerable<char> but a scalar; byte[] is bytes; a dict / Data-or-item sequence was handled
        // above. Without this a read-only domain collection would slip past narrowing to the entity
        // dispatch below, whose identity door answers "list" but can't build it — an infinite rebuild.
        if (raw is System.Collections.IEnumerable seq && raw is not string && raw is not byte[])
        {
            var l = new System.Collections.Generic.List<object?>();
            foreach (var e in seq) l.Add(e);
            return new global::app.type.item.list.@this(l, context.App.type.list["list"]);
        }

        // A CLR enum IS plang's choice (a closed named set) — build choice<T> for the enum. BEFORE
        // the index ask: an enum is neither an item nor _clr-owned, so the door would answer clr.
        if (raw is System.Enum && typeof(global::app.type.item.choice.@this<>).MakeGenericType(raw.GetType()) is var choice)
            return (@this)System.Activator.CreateInstance(choice, raw, context.App.type.list[choice])!;

        // The natural lift, NAVIGATED off the identity door: the owning entity drives its own cached
        // Create thunk (int → number, string → text). The door is never null — a CLR type no value
        // type owns answers the clr entity, whose Create builds the carrier — so this is the single
        // terminal rung; the old separate Clr fallback dissolves into entity dispatch.
        return context.App.type.list[raw.GetType()].Make(raw, context);
    }

    /// <summary>The birth of an item with a DECLARED type (decisions 786/787): an item declared with a KIND
    /// is made from its raw by that declaration, lazily — the raw's characters ride to the declared type's
    /// own wire-raw birth (<c>declared.Make</c>, <c>type/this.cs:378</c>): a fresh source read on first use
    /// through its kind, so <c>{item, kind: json}</c> holds its characters until used and is not text. A
    /// kindless item is the interface default (pass-through, a container deserializes itself, else declined).
    /// Only item's family comes here — a number or duration kind (decimal, iso) is its own family's birth.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        // item makes its OWN — a value already an item, its own kinds ({item, kind: json}), a container that
        // deserializes itself. A declaration whose class is ANOTHER type is not item's to make: it reaches item
        // only because that type's most-derived ICreate is item (a {table, xlsx}, whose table ships no reader),
        // and item declines so the owner's own Create or the default's CreateItemDeclined answers — never item
        // passing a stray text or re-sourcing the same bytes through as that other type.
        if (type is { } other && other.ClrType is { } clr && clr != typeof(@this))
        {
            data.Fail(new global::app.error.@this(
                $"%{data.Name}% is declared {other.Name} — item cannot make a {other.Name} from a {(raw as @this)?.Type.Name ?? raw?.GetType().Name}.",
                "CreateItemDeclined", 400));
            return null;
        }
        // The lazy-kind rebirth is for item's own kinds ({item, kind: json}): its characters ride to a fresh
        // source read through the kind.
        if (type is { } d && !d.kind.IsEmpty && raw is @this leaf && leaf.RawText is { } chars)
            return d.Make(chars, data.Context);
        if (raw is @this self) return self;
        if (raw is global::app.type.item.dict.@this or global::app.type.item.list.@this
            && ((@this)raw).Clr(typeof(@this)) is @this rec) return rec;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as @this)?.Type.Name ?? raw?.GetType().Name} — 'item' cannot be created from it.",
            "CreateItemDeclined", 400));
        return null;
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
    /// <c>data.Fail(error)</c> and answers the Data's empty value (its app's null). A truly
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
    /// which would re-run the rank pick and recurse. The asker's <paramref name="context"/> rides
    /// along for a container, whose raw items are handed out with it.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Compare(@this other, global::app.actor.context.@this context)
        => Rank >= other.Rank
            ? await Order(other, context)
            : global::app.data.ComparisonExtensions.Invert(await other.Order(this, context));

    /// <summary>
    /// The driver's per-type comparison: coerce <paramref name="other"/> into THIS kind (via the
    /// pure <c>Create</c> core) and order/equate in caller order. The base answers identity —
    /// equal to itself, else not-equal (a value with no order). A non-coercible other is
    /// <see cref="global::app.data.Comparison.Incomparable"/>, not an error. Async so a container
    /// can walk its items lazily (each pair awaited as reached, first mismatch exits). A scalar
    /// ignores <paramref name="context"/>; a container hands out its raw items with it.
    /// </summary>
    protected virtual System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(@this other, global::app.actor.context.@this context)
        => new(ReferenceEquals(this, other) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

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
    /// <para>The DEFAULT writes one of the value's own members — the ones its type lists — and the member
    /// writes itself (read as its type, then set): Ok, or why it couldn't. A thing that lives on keeps a member
    /// a program adds beside its own. Containers (<c>dict</c>/<c>list</c>) override with key/index writes.</para>
    /// (Distinct from <see cref="Output"/>, which serializes.)
    /// </summary>
    public virtual async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(string key, bool isIndex, object? value,
        global::app.actor.context.@this context)
    {
        if (!isIndex && Listed(key, context) is { } member) return await member.Write(this, value, context);
        if (!isIndex && Kept is { } kept)
        {
            kept.Add(key, value as global::app.data.@this ?? new global::app.data.@this(key, value, context: context));
            return context.Ok(this);
        }
        // Where a plain value holds a member a program adds is Ingi's open question; until it is answered it holds
        // none, and says so — a variable's own binding keeps the member instead.
        return context.Error(new global::app.error.@this(
            $"a {Type.Name} can't hold an added member yet ('{key}'); a dict can", "NoAddedMember", 404));
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
    /// property off THIS value — a domain item (<c>step.Index</c>, <c>goal.Step</c>) or a
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
    public virtual async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => await Member(parent, key) ?? On(parent, key) ?? await Setting(parent, key) ?? Kept?[key] ?? parent.Context.NotFound(key);

    // This value's events when the key is `on` — the event door, read by name though no member lists it.
    private global::app.data.@this? On(global::app.data.@this parent, string key)
        => string.Equals(key, "on", System.StringComparison.OrdinalIgnoreCase)
            ? parent.Context.App.type.list.Kind(on.GetType()).Data(parent.Name, on, parent, parent.Context)
            : null;

    /// <summary>The members this thing keeps beyond its own (<c>%!app.home%</c>), read after its own — a thing that
    /// lives on (the app, a call, an actor, a module, a goal, a step) keeps a list; a plain value keeps none.</summary>
    internal virtual global::app.type.item.kept.list.@this? Kept => null;

    /// <summary>The member of this value's own plang surface named <paramref name="key"/> — one its type lists (a list's
    /// <c>count</c>, <c>first</c>), read off the value: a property's value, or what a method answers when it asks for
    /// nothing a step must give. Null when the type lists none by that name; a value whose data reads through by key
    /// (a list's rows, a dict's entries) asks this, never every public member it has.</summary>
    private protected async System.Threading.Tasks.ValueTask<global::app.data.@this?> Member(global::app.data.@this parent, string key)
        => Listed(key, parent.Context) is { } member && member.Arguments?.All(argument => argument.Nullable) != false
            ? await member.Read(this, parent)
            : null;

    // The member named key of what this value is — its own class's list, the one its type's offer is made of. A name is
    // read ignoring its case, so two members it names is the error naming both, never one picked over the other.
    private global::app.type.property.@this? Listed(string key, global::app.actor.context.@this context)
    {
        var named = context.App.type.list.Member(GetType())
            .Where(member => string.Equals(member.Name, key, System.StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count > 1)
            throw new global::app.error.AppException(new global::app.error.@this(
                $"`{Type.Name}` has two members named {key}: " + string.Join(", ", named.Select(member => $"`{member.Member?.Name ?? member.Name}`")),
                "AmbiguousMember", 400));
        return named.FirstOrDefault();
    }

    /// <summary>This owner's settings when <paramref name="key"/> is <c>setting</c> (<c>%!app.goal.list.setting%</c>);
    /// null for any other key, or an owner with none.</summary>
    private protected async System.Threading.Tasks.ValueTask<global::app.data.@this?> Setting(global::app.data.@this parent, string key)
        => string.Equals(key, "setting", System.StringComparison.OrdinalIgnoreCase) ? await Setting(parent) : null;

    /// <summary>This owner's settings as the asker sees them — the class it names (<see cref="setting.ISetting{T}"/>);
    /// null when it names none. An owner whose settings are not one class (a module, an action) answers its own.</summary>
    protected virtual async System.Threading.Tasks.ValueTask<global::app.data.@this?> Setting(global::app.data.@this parent)
        => await parent.Context.Setting.Of(this);

    /// <summary>The child at <paramref name="key"/>, where <paramref name="isIndex"/> tells a position
    /// (<c>[0]</c>) from a member (<c>.name</c>) — the read twin of <see cref="Set"/>. A value that
    /// answers both the same way is navigated by key; a host with both faces (a sequence: element
    /// vs <c>.Count</c>) overrides.</summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key, bool isIndex)
        => Get(parent, key);

    /// <summary>The child at <paramref name="key"/> a write further in goes into — what <see cref="Get(global::app.data.@this, string, bool)"/>
    /// answers, for a value whose children are its own. A container whose rows are made at their read keeps the row
    /// it makes, so the write lands in the container.</summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Child(
        global::app.data.@this parent, string key, bool isIndex)
        => Get(parent, key, isIndex);

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

    /// <summary>Whether this value holds its children by position (a list) rather than by name (a dict, a
    /// json object, a domain value's members). A single value is not a sequence.</summary>
    public virtual bool IsSequence => false;

    /// <summary>This value as one no other holder's change can reach — a container (a dict, a list) copies what it
    /// holds, its nested containers copied too and every other value shared; any other value is itself. A task's
    /// first change to a container it read from its caller changes this copy, never the caller's.</summary>
    public virtual @this Copy() => this;

    // One slot of a container being copied: a nested container copied (raw or held), a Data a new Data (a write deeper
    // in rebinds the Data it lands on), every other value shared.
    private protected object? Copied(object? slot)
    {
        switch (slot)
        {
            case global::app.data.@this held:
                var value = held.Peek();
                var copy = value?.Copy();
                return copy == null || ReferenceEquals(copy, value) ? held.Copy(held.Name) : new global::app.data.@this(held.Name, copy, context: held.Context);
            case @this item:
                return item.Copy();
            case IDictionary<string, object?> raw:
                var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, nested) in raw) dictionary[key] = Copied(nested);
                return dictionary;
            case IList<object?> raw:
                return raw.Select(Copied).ToList();
            default:
                return slot;
        }
    }

    /// <summary>What this value becomes, written to a variable where <paramref name="previous"/> was — itself: a write
    /// replaces. A value that keeps what it replaces (a task keeps the tasks written before it) answers otherwise.
    /// The previous value is read only by a value that asks for it.</summary>
    public virtual System.Threading.Tasks.ValueTask<@this> Replace(System.Func<System.Threading.Tasks.ValueTask<@this?>> previous)
        => new(this);

    /// <summary>This value as named rows — each a Data under its name: a list its rows (a goal call's written
    /// parameters), a dict its entries (parameters given as one value at run); either may hold none. A single value is
    /// no rows at all: null.</summary>
    public virtual System.Collections.Generic.IEnumerable<global::app.data.@this>? Rows(global::app.actor.context.@this context) => null;

    /// <summary>
    /// Whether the holding <c>Data</c> may keep (rebind to) <see cref="Value"/>'s
    /// answer. True when the answer depends on nothing but the value itself
    /// (parse). False when the answer depends on outside state (a template
    /// render, a computed value) — those answer fresh at every use and are
    /// never kept.
    /// </summary>
    public virtual bool Cacheable => true;

    /// <summary>
    /// This value's history — the values it evolved THROUGH (a dict parsed from a file holds the
    /// file; an image born from a path holds the path). A value that never narrowed has an empty
    /// history. The narrowing/constructing type just <c>history.Add(prior)</c> — the history owns
    /// its add; <see cref="Is"/> queries it.
    /// </summary>
    private global::app.type.item.history.@this? _history;
    internal global::app.type.item.history.@this history => _history ??= new();

    /// <summary>
    /// This value's OWN type entity — the type that MADE it, handed in at birth (the app's list for an
    /// origin birth via <c>Make</c>, or forwarded from the value it came from). The single owner of its
    /// identity; provenance lives on the value's <see cref="list"/> history and is asked via <see cref="Is"/>.
    /// <para>Only the root type and the type list are born without one. A value whose maker does not hand its type
    /// yet answers the rebuild; that goes once every maker hands one.</para>
    /// </summary>
    private readonly global::app.type.@this? _type;

    protected internal virtual global::app.type.@this Type
        => _type ?? (FamilyOf(GetType()) is { } kind ? new(NameOf(kind.Family), kind.Family, kind.Kind) : new(GetType()));

    /// <summary>A value born holding the type that made it.</summary>
    protected @this(global::app.type.@this type) => _type = type;

    /// <summary>A value born without a type: the root type (its own type) and the type list (which answers its own
    /// type from itself) — the two makers of types. A value born through a C# implicit conversion reaches here too,
    /// its type rebuilt from its class, until the implicit-conversion branch removes those conversions.</summary>
    protected @this() { }

    /// <summary>
    /// Is this value (now or in its narrow history) an <paramref name="other"/>? Asks its type
    /// history — a <c>read config.json</c> that narrowed to a <c>dict</c> still answers
    /// <c>Is(file)</c> because the file rides in <see cref="list"/>.
    /// </summary>
    public bool Is(global::app.type.@this? other) => Type.Is(other) || history.Has(other);

    /// <summary>
    /// Is this value a <paramref name="name"/> — the word a program writes after <c>is</c>? A type's name asks the type
    /// (<see cref="Is(global::app.type.@this?)"/>), so a type wins over a kind by the same word; any other word is a
    /// kind, answered by <see cref="IsKind"/>: <c>%n% is int</c>, a read file <c>is json</c>, an archive <c>is gzip</c>.
    /// </summary>
    public bool Is(string name, global::app.type.list.@this types)
        => types.Contains(name) ? Is(types[name]) : IsKind(name);

    /// <summary>Does this value, as an answer writes it, hold the text a step quoted (<paramref name="quoted"/>)? By its
    /// own written form, by default — the build's check that an answer kept what the step said. A value that stands for
    /// what it doesn't write (a separator named <c>comma</c> stands for <c>,</c>) answers by that too. A value with rows (a
    /// list's elements, a dict's entries) also holds what any of them holds — each answers by its own, so a text inside
    /// a list is found by its characters, never by the escaped form the list writes it in.</summary>
    internal virtual async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
    {
        var writer = new global::app.goal.step.action.formal.Writer(context);
        await Output(writer);
        if (writer.ToString().Contains(quoted)) return true;
        foreach (var row in Rows(context) ?? [])
            if (await row.Value() is { } held && !ReferenceEquals(held, this) && await held.Holds(quoted, context)) return true;
        return false;
    }

    /// <summary>Does this value, as an answer writes it, hold the number a step wrote as digits? By its own written form,
    /// by default: the digits standing on their own, never inside a larger number (5 is not in 15). A value that spells
    /// a number another way (a duration of 1s holds 1000, as milliseconds) answers by that too. A value with rows also
    /// holds what any of them holds.</summary>
    internal virtual async System.Threading.Tasks.ValueTask<bool> Holds(global::app.type.item.number.@this number, global::app.actor.context.@this context)
    {
        var writer = new global::app.goal.step.action.formal.Writer(context);
        await Output(writer);
        if (System.Text.RegularExpressions.Regex.IsMatch(writer.ToString(),
                $@"(?<![\d.]){System.Text.RegularExpressions.Regex.Escape(number.ToString())}(?![\d.]|\.\d)")) return true;
        foreach (var row in Rows(context) ?? [])
            if (await row.Value() is { } held && !ReferenceEquals(held, this) && await held.Holds(number, context)) return true;
        return false;
    }

    /// <summary>Is this value of the kind <paramref name="name"/> (by its name, an alias, a MIME or an extension) — its
    /// own kind, or one a value in its history had. A value whose kinds are sizes answers every one it fits.</summary>
    protected internal virtual bool IsKind(string name) => Type.kind.Names(name) || history.HasKind(name);

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
    {
        // a copy is a new value: nothing is bound on it (its events would reach the actors and the app)
        var copy = (@this)MemberwiseClone();
        copy._on = null;
        var deep = Force.DeepCloner.DeepClonerExtensions.DeepClone(copy);
        deep._on = null;   // its class's shared ones, not a copy of the original's
        return deep;
    }

    private global::app.@event.on.@this? _on;

    /// <summary>This value's events (<c>%x.on.start%</c>) — its class's shared empty ones (the core's, and its type's
    /// declared ones) until something is bound on it. Not written with the value, and not taught as one of its properties.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.@event.on.@this on => _on ??= global::app.@event.on.@this.Of(GetType());

    /// <summary>The item whose events this one's fire through at <paramref name="depth"/>, outermost first — the
    /// general wrapping the specific, the last being the item itself; null past the last. An item on its own has
    /// one level, itself.</summary>
    protected internal virtual @this? Level(int depth, global::app.actor.context.@this context) => depth == 0 ? this : null;

    /// <summary>This value's own events, given to it by its first binding — what a binding is added to.</summary>
    internal global::app.@event.on.own Own()
    {
        if (_on is global::app.@event.on.own held) return held;
        var shared = on;
        var made = new global::app.@event.on.own(shared.Declared);
        return System.Threading.Interlocked.CompareExchange(ref _on, made, shared) is global::app.@event.on.own won
            ? won : made;
    }

    /// <summary>The variables this value holds, each once: a template's <c>%…%</c>, a container's
    /// entries', a variable itself. Read-only, born with the value; empty when none.</summary>
    public virtual IReadOnlyList<global::app.type.item.variable.@this> Variable => [];

    /// <summary>Whether the value holds a variable.</summary>
    public bool HasVariable => Variable.Count > 0;

    /// <summary>
    /// True when the value is already its own final answer — opening its door
    /// (<see cref="Value"/>) returns itself, no resolve/render/load. A literal
    /// scalar is final; a template renders, a file/url loads, a source parses, a
    /// computed answers fresh, so those are NOT final. Metadata/structural reads
    /// may take a final value as-is; a non-final value must be read through its
    /// door, and a carrier must never stand between the Data and it.
    /// </summary>
    internal virtual bool IsFinal => Type.Template == null;

    // A C# class's plang identity — its namespace name, and the family it is a kind of with the kind it is, when it is
    // one: pure facts of the class.
    private readonly record struct Identity(string Namespace, (System.Type Family, string Kind)? Family);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, Identity> _identities = new();

    private static Identity IdentityOf(System.Type t)
        => _identities.GetOrAdd(t, static ct =>
        {
            var ns = string.Join('.', (ct.Namespace ?? "").Split('.').Select(s => s.TrimStart('@')));
            var name = ct.Name.Split('`')[0];
            // A nested class answers to its owner's namespace, not the bare one — `decider+setting`'s
            // namespace is `app.module.llm.decider.setting`, never `app.module.llm.setting` (which is llm's own).
            var owner = ct.DeclaringType is { } d ? d.Name.Split('`')[0].TrimStart('@') + "." : "";
            var space = (name == "this" ? ns : ns.Length == 0 ? name : $"{ns}.{owner}{name}").ToLowerInvariant();
            for (var b = ct.BaseType; b != null; b = b.BaseType)
                if (b.IsDefined(typeof(global::app.Attributes.KindsAttribute), inherit: false))
                {
                    var called = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(ct, inherit: false)?.Name ?? space;
                    return new(space, (b, called[(called.LastIndexOf('.') + 1)..]));
                }
            return new(space, null);
        });

    /// <summary>The name a class's type goes by — the word it declares (<c>[PlangType("text")]</c>), else its
    /// namespace (<see cref="NamespaceOf"/>). Nothing is guessed from a folder.</summary>
    internal static string NameOf(System.Type t)
        => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(t, inherit: false)?.Name
           ?? NamespaceOf(t);

    /// <summary>The name the type of <paramref name="namespace"/> goes by — the <c>@this</c> item class there,
    /// its word else its namespace; null when no item class is the folder's.</summary>
    internal static string? NameOf(System.Reflection.Assembly assembly, string @namespace)
        => assembly.GetType(@namespace + ".this") is { } owner && typeof(@this).IsAssignableFrom(owner) ? NameOf(owner) : null;

    /// <summary>The identity of a class's type: its namespace — an <c>@this</c> class is its folder
    /// (<c>app.type.item.text</c>), any other class its folder and its name (<c>app.module.llm.toolcall</c>);
    /// lowercase, the <c>@</c> of a keyword folder dropped.</summary>
    internal static string NamespaceOf(System.Type t) => IdentityOf(t).Namespace;

    /// <summary>The family a class is a kind of, and the kind it is: the nearest base that declares it has kinds
    /// (<c>[Kinds]</c>) — a file is a kind of path, a where of query, a mouse of input — and the last word of the
    /// class's own name (<c>where</c>, <c>mouse</c>). Null for a class that is no family's kind.</summary>
    internal static (System.Type Family, string Kind)? FamilyOf(System.Type t) => IdentityOf(t).Family;

    /// <summary>A human-readable name for a type in an error — a plang <c>@this</c> type reads as its
    /// last one/two namespace segments (<c>step.list</c>, <c>action</c>, <c>item</c>), a CLR type
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
    /// What this value's <paramref name="name"/> field holds, read through its own navigation — or null when it
    /// has no such field. What <c>where</c> and <c>any</c> compare.
    /// </summary>
    public virtual async System.Threading.Tasks.Task<global::app.data.@this?> Field(string name, global::app.actor.context.@this context)
    {
        var held = await Get(new global::app.data.@this("", this, context: context), name);
        return held is { IsInitialized: true, Success: true } ? held : null;
    }

    /// <summary>The names of this value's fields, to tell a programmer which there are — none known by default.</summary>
    protected internal virtual System.Collections.Generic.IEnumerable<string> Fields => [];

    /// <summary>The answer when a <c>where</c> or <c>any</c> names a field no item has — a misspelling, an error
    /// naming it and the fields the items do have (<paramref name="known"/>).</summary>
    protected global::app.data.@this NoField(string name, System.Collections.Generic.IEnumerable<string> known,
        global::app.actor.context.@this context)
    {
        var names = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(known, System.StringComparer.OrdinalIgnoreCase));
        return context.Error(new global::app.error.ValidationError(
            $"No item has a field '{name}'" + (names.Count > 0 ? $" — the items have: {string.Join(", ", names)}" : ""), "FieldNotFound"));
    }

    /// <summary>
    /// What of this value holds when its <paramref name="field"/> is compared to <paramref name="value"/>
    /// under <paramref name="op"/> (<c>where %users% age &gt; 20</c>) — each type owns its answer: a list keeps
    /// the elements that do, a dict keeps itself or nothing. The default: a value with no fields has nothing
    /// to scope into.
    /// </summary>
    public virtual System.Threading.Tasks.Task<global::app.data.@this> Where(
        global::app.type.item.text.@this field, global::app.data.Operator op,
        global::app.data.@this value, global::app.actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(context.Error(new global::app.error.ValidationError(
            $"'where {field} …' needs a list or dict to scope into — a {Type.Name} has no fields.", "WhereOnApex")));

    /// <summary>Reads what this value stands for, as the one writing it out asks — at the last moment, by the
    /// channel about to send it; the value never loads itself while it writes. A value that holds all it is
    /// has nothing to read. The reason it couldn't be read, else null.</summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.error.@this?> Open(global::app.actor.context.@this context)
        => default;

    /// <summary>Whether this value, asked to become a <paramref name="asked"/> for <paramref name="data"/>,
    /// refuses — answering on <paramref name="data"/> itself. A value refuses nothing: the birth goes on.</summary>
    public virtual bool Refuses(global::app.type.@this asked, global::app.data.@this data) => false;

    /// <summary>This value, given by <paramref name="carrier"/>, goes into <paramref name="into"/> at
    /// <paramref name="at"/> (a position in it, else the end) — as one element pointing at the value's current
    /// instance. A list goes in as its elements instead.</summary>
    internal virtual void Enroll(global::app.type.item.list.@this into, global::app.data.@this carrier,
        global::app.type.item.number.@this at, global::app.actor.context.@this context)
        => into.Place(new global::app.data.@this(carrier.Name, carrier.Peek(), carrier.Type, context: context), at);

    /// <summary>This value, given by <paramref name="carrier"/>, spread into <paramref name="into"/> — itself, as
    /// one element. A list spreads its elements, however deep.</summary>
    internal virtual System.Threading.Tasks.ValueTask Spread(global::app.type.item.list.@this into,
        global::app.data.@this carrier, global::app.actor.context.@this context)
    {
        into.Add(carrier);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>
    /// Synchronous truthiness — each type's own answer, and emptiness is its negation (<c>isempty</c> is
    /// "not truthy"). The default: a present value is truthy; concrete types override (empty text, zero,
    /// false, an empty list or dict, and null — the absent item a missing value holds — are not).
    /// Whitespace is content: <c>"  "</c> is truthy. I/O truthiness (<c>path</c> existence) overrides
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

    /// <summary>The CLR exit door asked by a Data, with its context — a still-encoded value (a
    /// wire) decodes with it first; every other value lowers without one.</summary>
    internal virtual object? Clr(System.Type target, global::app.actor.context.@this? context) => Clr(target);

    /// <summary>Generic sugar over <see cref="Clr(System.Type)"/> — the
    /// compile-time-known-target form.</summary>
    internal T? Clr<T>() => (T?)Clr(typeof(T));

    /// <summary>
    /// This value read as another type, by that type's <paramref name="reader"/> — the same reader a <c>.pr</c>
    /// uses: the value is read as tokens (a dict an object, a list an array, a leaf its token), so a dict
    /// handed to a record's slot is the record its own reader makes. <paramref name="kind"/> is the declared
    /// type's element kind (<c>list&lt;action&gt;</c> → <c>action</c>), so a list reader loops the element's
    /// reader. A value that arrived in a format (a <c>clr</c> carrier holding json) bridges its format instead.
    /// </summary>
    internal virtual object? Read(global::app.type.reader.ITypeReader reader, string? kind,
                                  global::app.actor.context.@this context)
    {
        var tokens = new global::app.type.format.item.Reader(this, context);
        return reader.Read(ref tokens, kind, new global::app.type.reader.ReadContext(context));
    }

    /// <summary>
    /// The value's raw face for templates and CLR-facing probes (kind probes, ctor matching): a LEAF
    /// is its own backing via <see cref="Clr(System.Type)"/>; a container stays itself. A value whose
    /// raw face isn't its CLR backing answers its own — a choice is its name.
    /// </summary>
    internal virtual object? Backing => IsLeaf ? Clr<object>() : this;


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
    public virtual System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(IsTruthy());

    /// <summary>
    /// The lazy narrow: an un-narrowed value (<c>type=item, kind=json</c>) reads
    /// its raw form at first touch and re-stamps itself <c>dict</c>/<c>list</c>.
    /// The default — for an already-narrowed subtype — is a no-op that returns
    /// <c>self</c>, so a typed value carries no un-narrowed state.
    /// </summary>
    public virtual @this Narrow() => this;

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

    /// <summary>This value's own bytes, poured into <paramref name="into"/> as they come — what a digest of the value
    /// takes, so it matches any other tool's: a value's json text in its own key order (a dict's entries as they were
    /// added); a text, binary or file answers its own. Ok, or why the bytes could not be had (a file the actor may not
    /// read).</summary>
    public virtual async System.Threading.Tasks.Task<global::app.data.@this> Pour(System.IO.Stream into, actor.context.@this context)
    {
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(into))
            await Output(new global::app.type.item.kind.json.Writer(utf8, global::app.View.Out, context));
        return context.Ok();
    }

    /// <summary>A fact about the thing this value is, read with <c>!</c> (<c>%config!path%</c>) — null: a plain value
    /// has none, its members are read with a dot. A reference answers its own (a file's path, size, kind).</summary>
    internal virtual System.Threading.Tasks.ValueTask<global::app.data.@this?> Fact(string key, global::app.data.@this parent)
        => default;

    /// <summary>What packing this value writes into <paramref name="into"/>, and what that holds — so unpacking gives
    /// it back: a value packs as its Data whole, <paramref name="self"/>, in plang's own format (held: <c>data</c>); a
    /// file answers its contents and its name. Ok, or why it can't be packed.</summary>
    internal virtual async System.Threading.Tasks.Task<(global::app.data.@this result, string held, string? name)> Pack(
        global::app.data.@this self, System.IO.Stream into, actor.context.@this context)
        => (await context.App.type.list["wire"].kind["plang"]!.Encode(into, self, context, ct: context.CancellationToken), "data", null);

    /// <summary>
    /// This value as a diagnostic shows it — an assertion message, a test report line, a debug dump. A leaf
    /// answers its text; a structure writes itself through the json writer in the Debug view, where a
    /// <c>[Sensitive]</c> member shows masked: a reader tells "not set" from "set but hidden", and never sees
    /// the secret. Never a host's own ToString — a record's prints every field.
    /// </summary>
    public virtual async System.Threading.Tasks.ValueTask<string> Debug(global::app.actor.context.@this context)
    {
        if (IsLeaf) return ToString() ?? GetType().Name;
        using var stream = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
                     {
                         Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                     }))
            await Output(new global::app.type.item.kind.json.Writer(utf8, global::app.View.Debug, context));
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The item writes ITSELF into <paramref name="writer"/>: the writer hands it back its own write for the writer's
    /// own class (a text writer the text look, a formal writer the formal look, any other the value's own write). A
    /// value writing a member writes <c>member.Output(writer)</c>.
    /// </summary>
    public System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer) => writer.Write(this);

    /// <summary>
    /// The value's own write, in every format it has no look of its own for: what it is, through the writer's
    /// primitives (String, Number, Bytes, …); the writer decides how each looks. Every value with a form of its own (a
    /// leaf's one token, a structure's flat shape: error, path, permission, …) overrides this. The default is a
    /// structure's face for the writer's view: the tagged members of its <see cref="Host"/>, each under its wire name
    /// and nulls and empty lists left out, each member's value written as the plang value it is. A type of plang's own that declares no
    /// face for the view (a context, a callstack) is the view's to answer: the wire and the store refuse it, a dump
    /// names it. A foreign type can't declare, so it shows all its members. Resolved lazily as it reaches each node;
    /// <c>await</c>s happen here, between the writer's synchronous buffer writes.
    /// </summary>
    protected internal virtual async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var host = Host;
        var type = host.GetType();
        if (type.Assembly == typeof(@this).Assembly && !writer.View.Declares(type))
        {
            writer.View.Undeclared(host, writer);
            return;
        }
        writer.BeginObject();
        foreach (var entry in global::app.type.item.kind.reflection.Tagged.PropertiesFor(type, writer.View))
        {
            if (entry.Masked) { writer.Name(entry.WireName); writer.String("****"); continue; }
            object? value;
            try { value = entry.Property.GetValue(host); }
            catch (System.Exception ex)
            {
                await writer.View.Unread(entry, ex.InnerException ?? ex, writer);
                continue;
            }
            if (value == null) continue;
            // a member holding a Data writes it whole; any other is the plang value it is
            if (value is global::app.data.@this data)
            {
                writer.Name(entry.WireName);
                await data.Output(writer);
                continue;
            }
            var member = Create(value, writer.Context);
            // an empty list says nothing, as a null says nothing
            if (member.IsSequence && !member.IsTruthy()) continue;
            writer.Name(entry.WireName);
            await member.Output(writer);
        }
        writer.EndObject();
    }

    /// <summary>The object whose tagged members the general write shows: the value itself, or the C# object a carrier
    /// holds.</summary>
    private protected virtual object Host => this;

    /// <summary>How this value looks in text, where it looks different there (a type is its word); by default its own
    /// write.</summary>
    protected internal virtual System.Threading.Tasks.ValueTask Write(global::app.type.item.text.Writer writer)
        => Write((global::app.type.format.IWriter)writer);

    /// <summary>How this value looks in formal, where it looks different there (an action is its call); by default its
    /// own write.</summary>
    protected internal virtual System.Threading.Tasks.ValueTask Write(global::app.goal.step.action.formal.Writer writer)
        => Write((global::app.type.format.IWriter)writer);

    /// <summary>How this value looks in json, where it looks different there (characters that already are json are
    /// pasted); by default its own write.</summary>
    protected internal virtual System.Threading.Tasks.ValueTask Write(global::app.type.item.kind.json.Writer writer)
        => Write((global::app.type.format.IWriter)writer);

    /// <summary>How this value looks in plang's own format, where it looks different there (characters that already
    /// are json are pasted); by default its own write.</summary>
    protected internal virtual System.Threading.Tasks.ValueTask Write(global::app.type.item.wire.kind.plang.Writer writer)
        => Write((global::app.type.format.IWriter)writer);
}
