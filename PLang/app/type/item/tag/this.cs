namespace app.type.item.tag;

/// <summary>
/// PLang <c>tag</c> value — a normalized label (test tags, capability tags, debug frame tags).
/// A scalar leaf that rides the wire as its text form. The tag owns the discipline nobody else
/// should scatter: it <b>normalizes ONCE at birth</b> (trim) and compares <b>case-insensitively</b>,
/// so a consumer never hand-writes <c>.ToLowerInvariant()</c> to match a tag (the raw hand-off smell).
///
/// <para>Behavior lives on the wrapper as a <c>: item.@this</c>. Equality is case-insensitive on the
/// normalized value; truthiness is non-empty. Backed by a <see cref="string"/> but a DISTINCT type
/// from <c>text</c> (a tag knows it is a tag).</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "skip";
    public static string Shape => "string";

    /// <summary>The normalized label — trimmed at birth; equality folds case.</summary>
    public string Value { get; }

    public @this(string value) { Value = Normalize(value); }

    private static string Normalize(string s) => s.Trim();

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(Value);
    protected internal override global::app.type.@this Type => new("tag", typeof(@this));

    /// <summary>THE PURE CORE — a <c>tag</c> passes through; a non-blank string / <c>text</c> becomes a
    /// tag (normalized once); blank/other declines (<c>null</c>). Context-free.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value is string s && !string.IsNullOrWhiteSpace(s) ? new @this(s) : null;
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline names the reason.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(new global::app.error.Error(
            $"Cannot make a tag from {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name ?? "null")} — expected a non-empty label.",
            "TagConversionFailed", 400));
        return null;
    }

    public static implicit operator string(@this t) => t.Value;

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    /// <summary>Bare label — the serializer renders this.</summary>
    public override string ToString() => Value;

    // ---- Truthiness (item): an empty label is falsy ----
    public override bool IsTruthy() => Value.Length > 0;

    // ---- Comparison — case-insensitive label order (so `sort` and equality agree) ----
    public override int Rank => 110;   // just above text (a tag-shaped text coerces into the tag)

    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = string.Compare(Value, b.Value, System.StringComparison.OrdinalIgnoreCase);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

    // ---- Equality (case-insensitive on the normalized value — the tag owns this once) ----
    public bool AreEqual(object? other) => other switch
    {
        @this t => Equals(t),
        string raw => string.Equals(Value, Normalize(raw), System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public bool Equals(@this? other) => other is not null
        && string.Equals(Value, other.Value, System.StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
}
