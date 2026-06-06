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
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue, System.IEquatable<@this>
{
    public static string Example => "true";
    public static string Shape => "bool";

    public bool Value { get; }
    public override object? ToRaw() => Value;

    public @this(bool value) { Value = value; }

    public static readonly @this True = new(true);
    public static readonly @this False = new(false);

    // Both directions are lossless (bool carries no precision), so the wrapper owns
    // its conversions and call sites stay clean: `.Ok(true)` constructs, `if (x.Value)`
    // reads. The explicit ==/!= overloads below disambiguate `x == true` (which would
    // otherwise be ambiguous between @this==@this via from-bool and bool==bool via to-bool).
    public static implicit operator bool(@this b) => b.Value;
    public static implicit operator @this(bool b) => b ? True : False;

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);
    public static bool operator ==(@this? a, bool b) => a is not null && a.Value == b;
    public static bool operator !=(@this? a, bool b) => !(a == b);
    public static bool operator ==(bool a, @this? b) => b == a;
    public static bool operator !=(bool a, @this? b) => !(b == a);

    /// <summary>The truthiness primitive bottoms out here — the raw bool it wraps.</summary>
    public override bool IsTruthy() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Bool(Value);

    /// <summary>Bare lowercase <c>true</c>/<c>false</c> — the serializer renders this.</summary>
    public override string ToString() => Value ? "true" : "false";

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
