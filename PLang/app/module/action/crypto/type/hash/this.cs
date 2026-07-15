namespace app.module.action.crypto.type.hash;

/// <summary>
/// PLang <c>hash</c> value — a cryptographic digest plus the algorithm that
/// produced it. The algorithm is the value's <c>kind</c> (sha256, keccak256),
/// so a digest knows how to be verified without the caller re-supplying the
/// algorithm separately (the decoupling that made <c>crypto.verify</c>
/// mismatch-prone).
///
/// <para>Crypto-owned: it lives under the module that produces it
/// (<c>crypto.hash</c> returns it) rather than in <c>app/type/</c>, which is
/// reserved for the builtin vocabulary. The registry still resolves it to
/// <c>hash</c> — the <c>@this</c>/last-namespace-segment convention is
/// location-independent.</para>
///
/// <para>Scalar, string-shaped: the wire/render form is the base64 digest
/// (matches the historical <c>crypto.verify</c> which round-trips through
/// <c>Convert.FromBase64String</c>). Mirrors the <c>image</c> precedent —
/// bytes-backed, base64-rendered — but the kind is <em>advertised</em>
/// (<see cref="Kinds"/>), not extension-derived, so there is no <c>Build</c>
/// hook. The supported-algorithm set IS the kind vocabulary; it lives here
/// so the <c>crypto.hash</c> switch and the advertised list can't drift.</para>
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "sha256 digest, base64";
    public static string Shape => "string";

    /// <summary>
    /// Advertised algorithm vocabulary the LLM catalog renders as the kinds of
    /// <c>hash</c>. Single source of truth — <c>crypto.hash</c>'s algorithm
    /// switch must stay a subset of this.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<string> Kinds { get; }
        = new[] { "keccak256", "sha256" };

    /// <summary>The raw digest bytes.</summary>
    [global::app.Out, global::app.Store]
    public byte[] Bytes { get; }

    /// <summary>The algorithm that produced the digest — also the value's kind.</summary>
    [global::app.Out, global::app.Store]
    public string Algorithm { get; }

    public @this(byte[] bytes, string algorithm)
    {
        Bytes = bytes ?? System.Array.Empty<byte>();
        Algorithm = (algorithm ?? "").ToLowerInvariant();
    }

    /// <summary>A hash's entity: the algorithm IS the kind.</summary>
    protected internal override global::app.type.@this Type
        => new("hash") { Kind = string.IsNullOrEmpty(Algorithm) ? null : new global::app.type.kind.@this(Algorithm) };

    /// <summary>Canonical string form — base64. The type owns both directions.</summary>
    public string ToBase64() => System.Convert.ToBase64String(Bytes);

    /// <summary>The hash renders itself as its base64 digest — uniform across
    /// formats (the algorithm rides as the value's kind on the type envelope).</summary>
    public override void Write(global::app.channel.serializer.IWriter writer) => writer.String(ToBase64());

    /// <summary>
    /// Parse a base64 digest into a <c>hash</c> of the given algorithm. The
    /// byte↔base64 conversion lives here (OBP — it's hash behavior), so callers
    /// like <c>crypto.verify</c> compare digests through the type rather than
    /// reaching for <c>Convert.FromBase64String</c> themselves. Throws
    /// <see cref="System.FormatException"/> on invalid base64.
    /// </summary>
    public static @this FromBase64(string base64, string algorithm)
        => new(System.Convert.FromBase64String(base64), algorithm);


    /// <summary>True when this digest's bytes equal another's.</summary>
    public bool DigestEquals(@this other)
        => other != null && Bytes.AsSpan().SequenceEqual(other.Bytes);

    // ---- Comparison (the unified hook — see app.type.compare; discovered via the
    // value-class fallback, since `hash` lives outside the app.type.* name map) ----

    /// <summary>Equality-only: same digest bytes → <c>Equal</c>, else <c>NotEqual</c>;
    /// a non-hash side compares by its base64 text form, else <c>Incomparable</c>.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        var ha = a as @this;
        var hb = b as @this;
        if (ha != null && hb != null)
            return ha.DigestEquals(hb)
                ? global::app.data.Comparison.Equal
                : global::app.data.Comparison.NotEqual;
        return global::app.data.Comparison.Incomparable;
    }

    public static implicit operator string(@this h) => h.ToBase64();
    public override string ToString() => ToBase64();
}
