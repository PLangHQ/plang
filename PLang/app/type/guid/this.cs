namespace app.type.guid;

/// <summary>
/// PLang <c>guid</c> value — backed by <see cref="System.Guid"/> (the CLR type
/// the <c>guid</c> name resolves to). A scalar leaf identifier: it carries no
/// sub-structure and rides the wire as its canonical 36-char text form
/// (<c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>).
///
/// <para>Behavior (equality, order, truthiness) lives on the wrapper as a
/// <c>: item.@this</c>. Equality/order are by the underlying Guid (Guid's own
/// ordering — deterministic, so <c>sort</c> and <c>if a &gt; b</c> agree).
/// <b>Truthiness policy: the empty guid is falsy</b>, any other is truthy —
/// matching the empty-is-falsy convention of the other scalars.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "550e8400-e29b-41d4-a716-446655440000";
    public static string Shape => "string";

    public System.Guid Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Guid(Value);
    protected internal override global::app.type.@this Mint() => new("guid", typeof(System.Guid));

    public @this(System.Guid value) { Value = value; }

    /// <summary>THE PURE CORE — a <c>guid</c> passes through; a CLR <see cref="System.Guid"/> or a
    /// guid string (canonical/braced/hyphenless) parses; anything else declines (<c>null</c>). A
    /// text-wrapped literal unwraps through <c>Clr&lt;object&gt;()</c>. Context-free (the parse is
    /// <c>Guid.TryParse</c>). Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(global::app.type.item.@this value)
    {
        if (value is @this self) return self;
        return value.Clr<object>() switch
        {
            System.Guid raw => new @this(raw),
            string s when System.Guid.TryParse(s.Trim(), out var v) => new @this(v),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a string that didn't parse vs a wrong type).</summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(value.Clr<object>() is string s
            ? new global::app.error.Error($"Cannot parse '{s}' as guid — expected a 36-char guid (e.g. 550e8400-e29b-41d4-a716-446655440000).", "GuidParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {value.Mint().Name} to guid.", "GuidConversionFailed", 400));
        return null;
    }

    // Both directions are lossless; the wrapper owns its conversions. Guid is a
    // value type so `g == null` is unambiguous (matches only @this==@this).
    public static implicit operator System.Guid(@this g) => g.Value;
    public static implicit operator @this(System.Guid g) => new(g);

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);
    public static bool operator ==(@this? a, System.Guid b) => a is not null && a.Value == b;
    public static bool operator !=(@this? a, System.Guid b) => !(a == b);
    public static bool operator ==(System.Guid a, @this? b) => b == a;
    public static bool operator !=(System.Guid a, @this? b) => !(b == a);

    /// <summary>Bare canonical form (e.g. <c>550e8400-e29b-41d4-a716-446655440000</c>) — the serializer renders this.</summary>
    public override string ToString() => Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Truthiness (item): the empty guid is falsy ----
    public override bool IsTruthy() => Value != System.Guid.Empty;

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks text — a guid-format text coerces into the guid.</summary>
    internal static int CompareRank => 45;

    /// <summary>Guid ordering in caller order; the other side coerces through this
    /// family's own Convert hook (guid text → guid). Non-coercible → Incomparable.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        var ca = CoerceOwn(a);
        var cb = CoerceOwn(b);
        if (ca == null || cb == null) return global::app.data.Comparison.Incomparable;
        var c = ca.Value.CompareTo(cb.Value);
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    private static @this? CoerceOwn(object? v) => v as @this
        ?? convert.@this.OfStatic(typeof(@this),
               global::app.type.item.@this.Backing(v), null, null)?.Peek() as @this;

    // ---- Equality (by value) ----
    public bool AreEqual(object? other) => other switch
    {
        @this g => Value == g.Value,
        System.Guid raw => Value == raw,
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
