namespace app.type.item;

/// <summary>
/// Rung 2 of the value model — a strongly-typed C# object plang can hold but
/// that has no item type of its own (third-party classes, deserialized POCOs,
/// infra collections). The instance underneath stays strongly typed:
/// <see cref="Peek"/>/<see cref="Clr"/> answer with it, so generic
/// navigation, rendering and comparison work on the real object. A type
/// graduates to its own item subclass only when a generic answer stops being
/// the true answer for it.
/// </summary>
public sealed class clr : @this, module.IContext
{
    public object Value { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    public clr(object value)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        // Nested Data is not a shape: a Data never rides as a value (Lift forbids
        // it). The carrier is for foreign host objects only — carrying a Data here
        // is the courier debt, now abolished. Loud on any regression.
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A Data may not be carried in a clr — nested Data is not a supported shape. "
                + "Return the inner value via its own factory, never wrap a Data.");
    }

    /// <summary>
    /// A foreign host object is the apex of the value lattice — the un-narrowed
    /// <c>item</c> (≈ C# <c>object</c>) — so it reports <c>type=item</c> with its
    /// C# identity in <c>kind</c>: the declared <see cref="app.Attributes.PlangTypeAttribute"/>
    /// / registry short name when the class is named PLang vocabulary
    /// (<c>app</c>, <c>callstack</c>, …), else the version-independent
    /// <c>FullName</c> (any third-party POCO). This mirrors <c>number</c>/<c>int</c>:
    /// type = the lattice position, kind = the specialization.
    /// </summary>
    protected internal override global::app.type.@this Mint()
    {
        var clrType = Value.GetType();
        var kind = Context?.App.Type.ResolveName(clrType)
                   ?? clrType.FullName
                   ?? clrType.Name;
        return new global::app.type.@this("item", kind);
    }

    /// <summary>In memory now = the carrier itself (a closed box, like every
    /// other item whose <c>Peek</c> answers self). The carried host object is
    /// reachable ONLY through the explicit <c>.Clr&lt;T&gt;()</c> exit (leaf /
    /// .NET-boundary code), so no relay layer can reach past the carrier.</summary>
    public override object? Peek() => this;

    /// <summary>Shared by reference — the carrier holds a LIVE host object (the
    /// app singleton, a CallStack), and the value model says binding it shares
    /// the same live thing. Deep-cloning would walk the whole App graph (and the
    /// Context that rides for kind resolution) and overflow.</summary>
    protected internal override @this Clone() => this;

    /// <summary>
    /// A clr carrier has no wire form of its own — it is the rung-2 "I don't
    /// have a plang type" parking spot, and per the type rule a value that
    /// crosses the wire must be a real item that renders itself. Reaching here
    /// means a producer parked a non-item CLR object in a clr and then tried to
    /// serialize it; name the carried type so the producer is findable.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
        => throw new System.NotSupportedException(
            $"clr carrier wrapping '{Value.GetType().FullName}' reached the wire — "
            + "it has no plang type to render itself. Wrap it in a real item type, or fix the producer that parked it in a clr.");

    /// <summary>
    /// The carrier owns navigation into its host (behaviour on the element, never
    /// in a generic relay). A nested Data riding the carrier navigates as the real
    /// object; otherwise the host's property is reflected and re-wrapped (a nested
    /// host re-Lifts to a carrier, a scalar to its real item). Absent → NotFound,
    /// so the caller falls through. The inheritance walk is bottom-up + DeclaredOnly
    /// so a shadowing derived property wins and GetProperty never throws Ambiguous.
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
        if (prop == null) return global::app.data.@this.NotFound(key);

        try
        {
            var resolved = prop.GetValue(Value);
            // Relay an already-Data property value rather than re-boxing it (Rule #7).
            return resolved is global::app.data.@this d
                ? d
                : new global::app.data.@this(key, resolved, parent: parent);
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            return global::app.data.@this.FromError(new global::app.error.ServiceError(
                $"Failed to read '{key}': {(ex.InnerException ?? ex).Message}",
                "NavigationError", 500) { Exception = ex });
        }
    }

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    public override string ToString() => Value.ToString() ?? "";
    public override bool Equals(object? obj) =>
        obj is clr other ? Equals(Value, other.Value) : Equals(Value, obj);
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// The carrier writes its HOST to the wire — a foreign object has no plang shape of
    /// its own, so clr renders it as an object of its <c>[Out]</c> fields. Each field
    /// VALUE is raw CLR: it's lifted to its plang item (<see cref="global::app.type.@this.Create"/>)
    /// and THAT item writes itself. So clr owns only the reflection; every field's
    /// serialization is owned by its own item (text/number/dict/…), not by clr. The
    /// writer renders the emitted tokens per format (json/plang/protobuf).
    /// </summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
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
            // The field value is raw CLR — lift it to its item and let that item write
            // itself. (A Data field rides via its own self-describing Output.)
            if (raw is global::app.data.@this nested)
                await nested.Output(writer, mode, context);
            else
                await global::app.type.@this.Create(raw, context).Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
