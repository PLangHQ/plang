namespace app.type.@bool;

/// <summary>
/// PLang <c>bool</c> value — the truthiness primitive, backed by a raw CLR
/// <see cref="bool"/>. This is where the <see cref="global::app.data.IBooleanResolvable"/>
/// turtles stop: every other type's truthiness may delegate; <c>bool</c>'s
/// <see cref="IsTruthy"/> <em>is</em> the value it wraps.
///
/// <para><b>Equality-only.</b> <c>bool</c> deliberately does NOT implement
/// <see cref="global::app.data.IOrderableValue"/> — there is no natural order,
/// so <c>Compare.Order(bool, bool)</c> throws, matching the equality-only policy
/// dict carries. The bare wire form is lowercase <c>true</c>/<c>false</c>.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "true";
    public static string Shape => "bool";

    public bool Value { get; }
    protected internal override global::app.type.@this Mint() => new("bool", typeof(bool));

    public @this(bool value) { Value = value; }

    public static readonly @this True = new(true);
    public static readonly @this False = new(false);

    // INBOUND only — the entry lift (`.Ok(true)` constructs). The outbound
    // implicit (bool → CLR bool) is gone: every site was a silent CLR exit;
    // a reader names the bool face (`.Value`) at a real .NET edge. The ==/!=
    // overloads below keep `x == true` reading naturally without it.
    public static implicit operator @this(bool b) => b ? True : False;

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);
    public static bool operator ==(@this? a, bool b) => a is not null && a.Value == b;
    public static bool operator !=(@this? a, bool b) => !(a == b);
    public static bool operator ==(bool a, @this? b) => b == a;
    public static bool operator !=(bool a, @this? b) => !(b == a);

    // Boolean-context usage (if/while/&&/||) WITHOUT downgrading to CLR bool — the
    // value stays @bool everywhere except an explicit ==/.Value read. Deliberately
    // NOT `implicit operator bool`, which would silently degrade @bool at every boundary.
    public static bool operator true(@this b) => b.Value;
    public static bool operator false(@this b) => !b.Value;

    /// <summary>The CLR exit door — bool hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    /// <summary>
    /// THE PURE CORE — "bool, make yourself from this value, or decline." A <c>bool.@this</c>
    /// passes through; the value's backing parses (<c>"true"</c>/<c>"false"</c>, case-insensitive,
    /// or a raw CLR bool). <c>null</c> = decline (not this shape) — no Data, no error side-channel.
    /// Shared by the ICreate courier and (after the compare pass) comparison coercion.
    /// </summary>
    public static @this? Create(global::app.type.item.@this value)
    {
        if (value is @this self) return self;
        return value.Clr<object>() switch
        {
            bool b => (@this)b,
            string s when bool.TryParse(s, out var parsed) => (@this)parsed,
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — construction with a binding. Delegates to the pure core;
    /// on a decline it lands the reason on <paramref name="data"/> (a text that didn't parse vs a
    /// wrong type). A type with a kind (number precision, image format) reads it off
    /// <c>data.Type.Kind</c> here — bool has none, so the core's default construction stands.</summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(value.Clr<object>() is string s
            ? new global::app.error.Error($"Cannot parse '{s}' as bool — expected true or false.", "BoolParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {value.Mint().Name} to bool.", "BoolConversionFailed", 400));
        return null;
    }

    /// <summary>The truthiness primitive bottoms out here — the raw bool it wraps.</summary>
    public override bool IsTruthy() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Bool(Value);

    /// <summary>Bare lowercase <c>true</c>/<c>false</c> — the serializer renders this.</summary>
    public override string ToString() => Value ? "true" : "false";

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks text — `"true"` coerces into the bool, not vice versa.</summary>
    public override int Rank => 200;

    /// <summary>Equality-only: <c>Equal</c>/<c>NotEqual</c>, never an order — the
    /// boundary errors on <c>&lt;</c>/<c>&gt;</c>. The other side coerces into bool through
    /// the pure <c>Create</c> core ("true" → true). Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        return new(b is null ? global::app.data.Comparison.Incomparable
                 : Value == b.Value ? global::app.data.Comparison.Equal
                 : global::app.data.Comparison.NotEqual);
    }

    public bool AreEqual(object? other) => other switch
    {
        @this b => Value == b.Value,
        bool b => Value == b,
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
