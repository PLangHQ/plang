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
    private readonly string? _declared;
    private readonly string? _declaredKind;
    private readonly bool _declaredStrict;

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    public clr(object value, string? declaredTypeName = null, string? declaredKind = null, bool declaredStrict = false)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        _declared = declaredTypeName;
        _declaredKind = declaredKind;
        _declaredStrict = declaredStrict;
    }

    /// <summary>
    /// The carried class's own identity: the registry name when the class is
    /// registered, else its CLR name — the carrier is transparent. A courier
    /// label (the compress Wrap outer's declared category) overrides;
    /// transitional, dies with the schema layers.
    /// </summary>
    protected internal override global::app.type.@this Mint()
    {
        var clrType = Value.GetType();
        if (_declared != null)
            return new global::app.type.@this(_declared, _declaredKind, _declaredStrict);
        var name = Context?.App.Type.Name(clrType)
                   ?? global::app.type.catalog.@this.GetPrimitiveName(clrType)
                   ?? clrType.Name.ToLowerInvariant();
        return new global::app.type.@this(name, clrType);
    }

    /// <summary>A re-declared carrier — same carried object, new label
    /// (the declared judgement lands on the carrier, never on the object).</summary>
    internal clr Labeled(string typeName, string? kind, bool strict = false)
        => new(Value, typeName, kind, strict) { Context = Context };

    /// <summary>In memory now = the carried CLR object — existing consumers
    /// keep seeing the real instance, not the carrier. (Tightening the door to
    /// answer the carrier itself is deferred — too many raw-shape consumers
    /// remain; tracked on the slice list.)</summary>
    public override object? Peek() => Value;

    /// <summary>
    /// A clr carrier has no wire form of its own — it is the rung-2 "I don't
    /// have a plang type" parking spot, and per the type rule a value that
    /// crosses the wire must be a real item that renders itself. Reaching here
    /// means a producer parked a non-item CLR object in a clr and then tried to
    /// serialize it; name the carried type so the producer is findable.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
        => throw new System.NotSupportedException(
            $"clr carrier wrapping '{Value.GetType().FullName}' (declared '{_declared ?? "?"}') reached the wire — "
            + "it has no plang type to render itself. Wrap it in a real item type, or fix the producer that parked it in a clr.");

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    public override string ToString() => Value.ToString() ?? "";
    public override bool Equals(object? obj) =>
        obj is clr other ? Equals(Value, other.Value) : Equals(Value, obj);
    public override int GetHashCode() => Value.GetHashCode();
}
