namespace app.module.crypto.type.hash;

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
/// bytes-backed, base64-rendered — but the kind is an algorithm, one kind class
/// per algorithm under <c>hash/kind/</c>, not extension-derived, so there is no
/// <c>Build</c> hook.</para>
/// </summary>
[global::app.Attributes.PlangType("hash")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=";
    public static string Description => "A cryptographic digest in base64, with the algorithm that made it (sha256, keccak256).";
    public static string Shape => "string";

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
        => new("hash", typeof(@this), Algorithm);

    /// <summary>Canonical string form — base64. The type owns both directions.</summary>
    public string ToBase64() => System.Convert.ToBase64String(Bytes);

    /// <summary>The hash renders itself as its base64 digest — uniform across
    /// formats (the algorithm rides as the value's kind on the type envelope).</summary>
    public override void Write(global::app.type.format.IWriter writer) => writer.String(ToBase64());

    /// <summary>A hash writes its own form — its base64 digest (<see cref="Write"/>) — in every view.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>The hash of <paramref name="bytes"/> by <paramref name="algorithm"/> — the kind of hash digests them
    /// itself (<c>hash/kind/sha256</c>, <c>hash/kind/keccak256</c>).</summary>
    internal static @this Of(byte[] bytes, kind.@this algorithm) => new(algorithm.Digest(bytes), algorithm.Name);

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

    // ---- Comparison: the digest reads the other side into a digest and compares bytes ----

    /// <summary>A digest drives a comparison with a text, a base64 or a binary: the other side is read into a
    /// digest (above text 100, base64 200, binary 250).</summary>
    public override int Rank => 260;

    /// <summary>
    /// THE PURE CORE — a digest passes through; a text is read as a digest's text: hex (two characters a byte),
    /// else base64. The algorithm is not in the text, so a digest read from one has none — a comparison asks by
    /// bytes. Anything else declines.
    /// </summary>
    public static @this? Create(object? raw) => Digest(raw, length: null);

    /// <summary>Equality only — a digest has no order: the other side read into a digest (its text as hex when it
    /// is hex of this digest's length, else as base64), then the bytes compared. A text that is neither, or any
    /// other value, is <c>Incomparable</c>.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
        global::app.type.item.@this other, global::app.actor.context.@this context)
        => new(Digest(other, Bytes.Length) is { } digest
            ? DigestEquals(digest) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual
            : global::app.data.Comparison.Incomparable);

    // The digest a value stands for: a digest is itself; a text is its hex when it is hex (of `length` bytes, when the
    // length is known), else its base64 — written `<algorithm>:<digest>` (sha256:<hex>), the algorithm is the prefix's;
    // null when it is neither, or no text.
    private static @this? Digest(object? raw, int? length)
    {
        if (raw is @this digest) return digest;
        if (raw is not global::app.type.item.text.@this text) return null;
        var characters = text.ToString();
        var algorithm = "";
        if (characters.IndexOf(':') is > 0 and var colon && characters[..colon].All(char.IsLetterOrDigit))
        {
            algorithm = characters[..colon];
            characters = characters[(colon + 1)..];
        }
        if (characters.Length > 0 && characters.Length % 2 == 0 && (length is null || characters.Length == length * 2)
            && characters.All(System.Uri.IsHexDigit))
            return new @this(System.Convert.FromHexString(characters), algorithm);
        return System.Buffers.Text.Base64.IsValid(characters) && characters.Length > 0
            ? new @this(System.Convert.FromBase64String(characters), algorithm)
            : null;
    }

    /// <summary>The birth: a digest as <see cref="Create(object?)"/> reads it — and one whose written algorithm
    /// (<c>md5:…</c>) is no kind of hash is refused, naming the kinds.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Digest(raw, length: null) is not { } digest)
        {
            data.Fail(new global::app.error.Error(
                $"%{data.Name}% is no digest — a hash is written as hex or base64, or <algorithm>:<digest> (sha256:<hex>)",
                "CreateItemDeclined", 400));
            return null;
        }
        var kinds = data.Context!.App.type.list["hash"].kind.list(data.Context).Items().Select(t => t.kind.Name).ToList();
        if (digest.Algorithm.Length > 0 && !kinds.Contains(digest.Algorithm, StringComparer.OrdinalIgnoreCase))
        {
            data.Fail(new global::app.error.Error(
                $"'{digest.Algorithm}' is no kind of hash — the kinds are {string.Join(", ", kinds)}", "HashInvalid", 400));
            return null;
        }
        return digest;
    }

    /// <summary>The digest written as <c>&lt;algorithm&gt;:&lt;hex&gt;</c> — how a mismatch names it.</summary>
    public string Written => $"{Algorithm}:{System.Convert.ToHexString(Bytes).ToLowerInvariant()}";

    /// <summary>Why <paramref name="actual"/> is not this digest, naming both (HashMismatch); null when it is.</summary>
    public global::app.error.Error? Mismatch(@this actual) => DigestEquals(actual) ? null
        : new global::app.error.Error($"the hash is {actual.Written}, expected {Written}", "HashMismatch", 400);

    public static implicit operator string(@this h) => h.ToBase64();
    public override string ToString() => ToBase64();
}
