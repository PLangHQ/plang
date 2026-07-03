namespace app.type.clr;

/// <summary>
/// Rung 2 of the value model — a strongly-typed C# object plang can hold but that has no
/// item type of its own (third-party classes, deserialized POCOs, infra collections). The
/// instance underneath stays strongly typed: <see cref="Peek"/>/<see cref="Clr"/> answer
/// with it, so generic navigation, rendering and comparison work on the real object. A type
/// graduates to its own item subclass only when a generic answer stops being the true
/// answer for it.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.actor.context.@this Context { get; set; } = null!;

    /// <summary>Born WITH context — the carrier navigates/serializes through it (child
    /// values, type resolution). A clr always wraps a host for a wired scope.</summary>
    public @this(object value, global::app.actor.context.@this context)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        // Nested Data is not a shape: a Data never rides as a value (Lift forbids it). The
        // carrier is for foreign host objects only — carrying a Data here is courier debt.
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A Data may not be carried in a clr — nested Data is not a supported shape. "
                + "Return the inner value via its own factory, never wrap a Data.");
    }

    /// <summary>
    /// A foreign host object is the apex of the value lattice — the un-narrowed <c>item</c>
    /// (≈ C# <c>object</c>) — so it reports <c>type=item</c> with its C# identity in
    /// <c>kind</c>: the declared <see cref="app.Attributes.PlangTypeAttribute"/> / registry
    /// short name when the class is named PLang vocabulary (<c>app</c>, <c>callstack</c>, …),
    /// else the version-independent <c>FullName</c>. Mirrors <c>number</c>/<c>int</c>: type =
    /// the lattice position, kind = the specialization.
    /// </summary>
    protected internal override global::app.type.@this Mint()
    {
        var clrType = Value.GetType();
        var kind = Context.App.Type.ResolveName(clrType)
                   ?? clrType.FullName
                   ?? clrType.Name;
        return new global::app.type.@this("item", kind);
    }

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
    /// The carrier owns navigation into its host (behaviour on the element). A nested Data
    /// riding the carrier navigates as the real object; otherwise the host's property is
    /// reflected and re-wrapped. Absent → NotFound. The walk is bottom-up + DeclaredOnly so a
    /// shadowing derived property wins and GetProperty never throws Ambiguous.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
    {
        if (Value is global::app.data.@this innerData)
            return await innerData.GetChild(key);

        System.Reflection.PropertyInfo? prop = null;
        for (var t = Value.GetType(); t != null && prop == null; t = t.BaseType)
            prop = t.GetProperty(key, System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.DeclaredOnly);
        if (prop == null) return Context.NotFound(key);

        try
        {
            var resolved = prop.GetValue(Value);
            return resolved is global::app.data.@this d
                ? d
                : new global::app.data.@this(key, resolved, parent: parent, context: Context);
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            return Context.Error(new global::app.error.ServiceError(
                $"Failed to read '{key}': {(ex.InnerException ?? ex).Message}",
                "NavigationError", 500) { Exception = ex });
        }
    }

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

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
    /// The carrier writes its HOST to the wire. For a divergent format it uses that format's
    /// serializer (text → json string). Otherwise (json/plang) a foreign object has no plang
    /// shape of its own, so it renders as an object of its <c>[Out]</c> fields — each field
    /// VALUE is raw CLR, lifted to its item via <see cref="global::app.type.@this.Create"/>,
    /// and THAT item writes itself. So clr owns only the reflection; every field's
    /// serialization is owned by its own item.
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
        writer.BeginObject();
        foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(Value.GetType(), mode))
        {
            writer.Name(entry.Property.Name.ToLowerInvariant());
            if (entry.Masked) { writer.String("****"); continue; }
            object? raw;
            try { raw = entry.Property.GetValue(Value); }
            catch (System.Exception ex)
            {
                throw new global::app.data.OutputException(
                    $"Output failed reading {Value.GetType().Name}.{entry.Property.Name}: {ex.Message}",
                    "OutputGetterThrew", ex);
            }
            if (raw is global::app.data.@this nested)
                await nested.Output(writer, mode, context);
            else
                await global::app.type.@this.Create(raw, context).Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
