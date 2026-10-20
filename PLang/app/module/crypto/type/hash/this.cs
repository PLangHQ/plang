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
    public static string Shape => "string";

    /// <summary>The raw digest bytes.</summary>
    [global::app.Out, global::app.Store]
    public byte[] Bytes { get; }

    /// <summary>The kind of hash that produced the digest — also the value's kind; null for a digest read from bare
    /// text (a comparison's), whose algorithm isn't written.</summary>
    public kind.@this? Algorithm { get; }

    /// <summary>A digest by <paramref name="algorithm"/>, born holding <paramref name="type"/> — the app's
    /// <c>{hash, &lt;algorithm&gt;}</c>: the algorithm IS the kind.</summary>
    public @this(byte[] bytes, kind.@this? algorithm, global::app.type.@this type) : base(type)
    {
        Bytes = bytes ?? System.Array.Empty<byte>();
        Algorithm = algorithm;
    }

    /// <summary>A digest its kind takes, where no app is in reach (a kind's own digest): it holds {hash, <algorithm>}
    /// built from the class — until the kinds item finds the app's through the kind's family.</summary>
    public @this(byte[] bytes, kind.@this? algorithm)
        : this(bytes, algorithm, new global::app.type.@this("hash", typeof(@this), algorithm?.Name)) { }

    /// <summary>Canonical string form — base64. The type owns both directions.</summary>
    public string ToBase64() => System.Convert.ToBase64String(Bytes);

    /// <summary>The hash renders itself as its base64 digest — uniform across
    /// formats (the algorithm rides as the value's kind on the type envelope).</summary>
    /// <summary>A hash writes its own form — its base64 digest — in every view.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(ToBase64());
        return default;
    }

    /// <summary>The hash of <paramref name="bytes"/> by <paramref name="algorithm"/> — the kind of hash digests them
    /// itself (<c>hash/kind/sha256</c>, <c>hash/kind/keccak256</c>).</summary>
    internal static @this Of(byte[] bytes, kind.@this algorithm) => new(algorithm.Digest(bytes), algorithm);

    /// <summary>
    /// Parse a base64 digest into a <c>hash</c> of the given algorithm. The
    /// byte↔base64 conversion lives here (OBP — it's hash behavior), so callers
    /// like <c>crypto.verify</c> compare digests through the type rather than
    /// reaching for <c>Convert.FromBase64String</c> themselves. Throws
    /// <see cref="System.FormatException"/> on invalid base64.
    /// </summary>
    public static @this FromBase64(string base64, kind.@this? algorithm)
        => new(System.Convert.FromBase64String(base64), algorithm);


    /// <summary>True when this digest's bytes equal another's.</summary>
    public bool DigestEquals(@this other)
        => other != null && Bytes.AsSpan().SequenceEqual(other.Bytes);

    // ---- Comparison: the digest reads the other side into a digest and compares bytes ----

    /// <summary>A digest drives a comparison with a text, a base64 or a binary: the other side is read into a
    /// digest (above text 100, base64 200, binary 250).</summary>
    public override int Rank => 260;

    /// <summary>Equality only — a digest has no order: the other side read into a digest (its text as hex when it
    /// is hex of this digest's length, else as base64), then the bytes compared. A text that is neither, or any
    /// other value, is <c>Incomparable</c>.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
        global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        @this? digest;
        try { digest = Digest(other, Bytes.Length, context); }
        catch (System.FormatException) { digest = null; }   // an algorithm that is no kind of hash compares to nothing
        return new(digest is { } read
            ? DigestEquals(read) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual
            : global::app.data.Comparison.Incomparable);
    }

    // The digest a value stands for: a digest is itself; a text is its hex when it is hex (of `length` bytes, when the
    // length is known), else its base64 — written `<algorithm>:<digest>` (sha256:<hex>), the algorithm is the prefix's,
    // read as a kind the asker's hash type names (an unknown name is a FormatException naming the kinds); null when it
    // is neither, or no text.
    private static @this? Digest(object? raw, int? length, global::app.actor.context.@this context)
    {
        if (raw is @this digest) return digest;
        if (raw is not global::app.type.item.text.@this text) return null;
        var characters = text.ToString();
        kind.@this? algorithm = null;
        if (characters.IndexOf(':') is > 0 and var colon && characters[..colon].All(char.IsLetterOrDigit))
        {
            var type = context.App.type.list[typeof(@this)];
            algorithm = type.kind[characters[..colon]] as kind.@this
                ?? throw new System.FormatException($"'{characters[..colon]}' is no kind of hash. Valid: "
                    + string.Join(", ", type.kind.list(context).Items().Select(t => t.kind.Name).Where(n => n.Length > 0)));
            characters = characters[(colon + 1)..];
        }
        // born holding the asker's {hash, <algorithm>}
        var typed = context.App.type.list[new global::app.type.@this("hash", algorithm?.Name), context];
        if (characters.Length > 0 && characters.Length % 2 == 0 && (length is null || characters.Length == length * 2)
            && characters.All(System.Uri.IsHexDigit))
            return new @this(System.Convert.FromHexString(characters), algorithm, typed);
        return System.Buffers.Text.Base64.IsValid(characters) && characters.Length > 0
            ? new @this(System.Convert.FromBase64String(characters), algorithm, typed)
            : null;
    }

    /// <summary>The birth: a digest passes through; a text is read as a digest's text — hex (two characters a byte),
    /// else base64, an algorithm it names a kind of hash (one that names no kind, <c>md5:…</c>, is refused, naming the
    /// kinds); a digest whose text names no algorithm has none, and a comparison asks by bytes.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        @this? digest;
        try { digest = Digest(raw, length: null, data.Context); }
        catch (System.FormatException unknown)
        {
            data.Fail(new global::app.error.@this(unknown.Message, "HashInvalid", 400));
            return null;
        }
        if (digest is null)
        {
            data.Fail(new global::app.error.@this(
                $"%{data.Name}% is no digest — a hash is written as hex or base64, or <algorithm>:<digest> (sha256:<hex>)",
                "CreateItemDeclined", 400));
            return null;
        }
        return digest;
    }

    /// <summary>The digest written as <c>&lt;algorithm&gt;:&lt;hex&gt;</c> — how a mismatch names it.</summary>
    public string Written => $"{Algorithm?.Name}:{System.Convert.ToHexString(Bytes).ToLowerInvariant()}";

    /// <summary>Why <paramref name="actual"/> is not this digest, naming both (HashMismatch); null when it is.</summary>
    public global::app.error.@this? Mismatch(@this actual) => DigestEquals(actual) ? null
        : new global::app.error.@this($"the hash is {actual.Written}, expected {Written}", "HashMismatch", 400);

    public static implicit operator string(@this h) => h.ToBase64();
    public override string ToString() => ToBase64();
}
