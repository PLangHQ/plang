namespace app.type.clr;

/// <summary>
/// Rung 2 of the value model — a strongly-typed C# object plang can hold but that has no
/// item type of its own (third-party classes, deserialized POCOs, infra collections). The
/// instance underneath stays strongly typed: <see cref="Peek"/>/<see cref="Clr"/> answer
/// with it, so generic navigation, rendering and comparison work on the real object. A type
/// graduates to its own item subclass only when a generic answer stops being the true
/// answer for it.
/// </summary>
public class @this : global::app.type.item.@this, global::app.module.IContext, global::app.type.item.ICreate<@this>
{
    /// <summary>The clr entity's construction face — wrap ANY foreign host in a carrier. A CLR type
    /// no value type owns IS clr(T), so this never declines for a real host; it is terminal, which
    /// is what lets the lift's clr rung be plain entity dispatch instead of a special case. The
    /// <c>new</c> hides item's apex lift (build-WHATEVER-the-raw-is) with the build-a-clr answer the
    /// "clr" entity owns; null raw is a citizen handled upstream, so it declines here.</summary>
    public static new @this? Create(object? raw, global::app.actor.context.@this? ctx)
        => raw is null ? null : new @this(raw, ctx!);

    public object Value { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.actor.context.@this Context { get; set; } = null!;

    /// <summary>
    /// The kind the carrier navigates AS — born at construction: the stamp from whoever
    /// wrapped the host and knew its format (the json reader/producer, <c>type.Create</c>
    /// for a <see cref="System.Text.Json.JsonElement"/>, a json container child), else
    /// resolved once from the host's CLR type (<c>JsonElement → json</c>, anything the kind
    /// system doesn't claim → <c>*</c> reflection). Never recomputed — the carrier just
    /// carries it, and asks it to navigate.
    /// </summary>
    /// <remarks>JsonIgnore: navigation metadata, not the carrier's wire shape — the clr
    /// renders its HOST (via <see cref="Output"/>), not its own kind. Reflecting it would
    /// pull the generic JSON serializer into the kind token's context-dependent accessors.</remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.type.kind.@this Kind { get; }

    /// <summary>Born WITH context — the carrier navigates/serializes through it (child
    /// values, type resolution). A clr always wraps a host for a wired scope. A producer
    /// may stamp a <paramref name="kind"/> when it knows the host's format; otherwise the
    /// kind system resolves it from the host's CLR type at birth.</summary>
    public @this(object value, global::app.actor.context.@this context, global::app.type.kind.@this? kind = null)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        // Nested Data is not a shape: a Data never rides as a value (Lift forbids it). The
        // carrier is for foreign host objects only — carrying a Data here is courier debt.
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A Data may not be carried in a clr — nested Data is not a supported shape. "
                + "Return the inner value via its own factory, never wrap a Data.");
        // Born kind: an explicit stamp, else the kind that CLAIMS this CLR form (json → its
        // JsonElement, a list → IList, …), else the `*` reflection kind. The one door answers
        // all three (exact → assignable → catch-all) and never null.
        Kind = kind ?? Context.App.Type.Kind[value.GetType()];
    }

    /// <summary>
    /// A foreign host is the apex of the value lattice — the un-narrowed <c>item</c> (≈ C#
    /// <c>object</c>) — so it reports <c>type=item</c> with its <see cref="Kind"/> as the
    /// specialization (<c>json</c> for a JsonElement, <c>*</c> for an unrecognised POCO).
    /// Mirrors <c>number</c>/<c>int</c>: type = the lattice position, kind = the format.
    /// </summary>
    protected internal override global::app.type.@this Type
        => new global::app.type.@this("item", Kind.Name);

    /// <summary>In memory now = the carrier itself (a closed box, like every other item whose
    /// <c>Peek</c> answers self). The carried host is reachable ONLY through the explicit
    /// <c>.Clr&lt;T&gt;()</c> exit, so no relay layer can reach past the carrier.</summary>
    public override object? Peek() => this;

    /// <summary>Shared by reference — the carrier holds a LIVE host (the app singleton, a
    /// CallStack); the value model says binding it shares the same live thing. Deep-cloning
    /// would walk the whole App graph and overflow.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>
    /// A clr carrier has no bare wire form — it is the rung-2 "I don't have a plang type"
    /// parking spot. Reaching here means a producer parked a non-item CLR object in a clr and
    /// tried to serialize it as a leaf; name the carried type so the producer is findable.
    /// (The clr's structured wire form is <see cref="Output"/>, which reflects the host.)
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
        => throw new System.NotSupportedException(
            $"clr carrier wrapping '{Value.GetType().FullName}' reached the wire as a leaf — "
            + "it has no plang type to render itself. Wrap it in a real item type, or fix the producer that parked it in a clr.");

    /// <summary>
    /// The carrier navigates its host by asking its own <see cref="Kind"/> — the json kind
    /// walks a JsonElement, the <c>*</c> kind reflects a POCO. A single key is a one-segment
    /// path; the kind owns the walk (and the path language).
    /// </summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => Get(parent, global::app.variable.path.@this.Parse(key));

    /// <summary>The whole-path handoff — the carrier hands its <see cref="Kind"/> the entire
    /// tail so the kind walks it in ONE call (and, later, in its OWN path language:
    /// jsonpath/css). <c>data.Get</c> hands the value-plane path here; infra/method
    /// segments stay on the generic per-hop walk.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, global::app.variable.path.@this path)
        => Kind.Get(Value, path, parent, Context);

    /// <summary>The child-write door — the carrier routes to its <see cref="Kind"/> (the * kind
    /// reflects a settable property, the list kind writes an index). The kind returns the value
    /// carried back as an item; a host mutates in place, so identity holds.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value)
    {
        // A clr host takes a CONCRETE child (a typed property / element), never a lazy Data — so a
        // Data value opens its own door to its value here. (dict/list hold Data lazily; a host can't.)
        if (value is global::app.data.@this dv) value = await dv.Value();
        return await Kind.Set(Value, key, isIndex, value, Context);
    }

    /// <summary>The children of the host, via its <see cref="Kind"/> — for <c>foreach</c>.</summary>
    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Kind.Enumerate(Value, Context);

    /// <summary>Iterates as (key, value) pairs — the carrier delegates enumeration to its
    /// <see cref="Kind"/> (json array elements / object members), pairing each with its key
    /// (array → index, object → member name). Mirrors list/dict; without it the base yields
    /// the whole host once, so <c>foreach %plan.steps%</c> would bind the array, not a step.</summary>
    public override System.Collections.Generic.IEnumerable<(global::app.data.@this key, global::app.data.@this value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        var ctx = context ?? Context;
        int i = 0;
        foreach (var element in Kind.Enumerate(Value, ctx))
        {
            var key = string.IsNullOrEmpty(element.Name)
                ? new global::app.data.@this("", i, context: ctx)
                : new global::app.data.@this("", element.Name, context: ctx);
            i++;
            yield return (key, element);
        }
    }

    // The kind owns the lower: identity is a no-op here; otherwise the kind builds (json →
    // reflection Read) or declares it can't (terminal). No shared ClrConvert — clr wraps a
    // JsonElement or a POCO, neither IConvertible, so there was never a ChangeType to do.
    internal override object? Clr(System.Type target)
        => target.IsInstanceOfType(Value) ? Value
         : Kind.Clr(Value, target, Context);

    public override string ToString() => Value.ToString() ?? "";
    public override bool Equals(object? obj) =>
        obj is Clr other ? Equals(Value, other.Value) : Equals(Value, obj);
    public override int GetHashCode() => Value.GetHashCode();

    // The carrier owns its per-format serializers — instantiated directly (no reflection, no
    // registry), keyed by format. Only formats that DIVERGE from the default reflection are
    // listed; text is here because a foreign host has no plain-text form (renders as json).
    private static readonly System.Collections.Generic.Dictionary<string, global::app.channel.serializer.IOutput> _formats
        = new() { ["text"] = new format.text() };

    /// <summary>
    /// The carrier writes its HOST to the wire by asking its <see cref="Kind"/> — the json
    /// kind emits raw json (no <c>valueKind</c> BCL leak), the <c>*</c> kind reflects a POCO's
    /// <c>[Out]</c> fields. A divergent channel format (text) still uses that format's own
    /// serializer (a foreign host has no plain-text form — renders as a json string).
    /// </summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (_formats.TryGetValue(writer.Format, out var serializer))
        {
            await serializer.Output(this, writer, mode, context);
            return;
        }
        await Kind.Output(Value, writer, mode, context ?? Context);
    }
}
