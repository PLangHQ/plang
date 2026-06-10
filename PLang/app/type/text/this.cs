namespace app.type.text;

/// <summary>
/// PLang <c>text</c> type — the canonical name for textual content (replaces
/// the historical primitive <c>string</c> on the PLang surface; <c>string</c>
/// stays as an accepted alias). Mirrors <c>image</c> but text-backed instead
/// of bytes-backed.
///
/// <para>Kind is open (no advertised vocabulary): a <c>text</c> value's kind
/// comes from the file extension (<c>md</c>, <c>txt</c>, <c>csv</c>, …) via
/// the <see cref="Build"/> hook. A plain string with no extension is
/// <c>text</c> with kind <c>null</c>.</para>
///
/// <para>The wire shape is just a string — <see cref="Shape"/> says <c>string</c>.
/// The backing CLR <c>string</c> is the single source of truth; behavior
/// (length, case, contains, compare, truthiness) is a member on the wrapper, so
/// the <c>is string</c> consumer-switches collapse into method calls.</para>
///
/// <para><b>Case policy.</b> Order and value-equality are <em>ordinal,
/// case-insensitive</em> — matching the historical <c>ScalarComparer</c> string
/// behavior so <c>"abc" == "ABC"</c> and sort order are unchanged after text
/// flows native. <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>
/// align with that policy so a <c>text</c> works as a <c>HashSet</c> member /
/// dict key without surprise.</para>
///
/// <para><b>Atomicity.</b> <c>text</c> is a scalar, not a sequence of chars —
/// it deliberately does <b>not</b> implement <c>IEnumerable</c>, so
/// <c>foreach %s%</c> never char-iterates it.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this,
    System.IEquatable<@this>
{
    public static string Example => "readme.md";
    public static string Shape => "string";
    /// <summary>
    /// LLM-facing teaching: text's kind comes from the file extension
    /// (`md`, `txt`, `csv`, `html`, …). The kind is a hint by default; strict
    /// is a no-op for text since plain vs markdown can't be probed from content.
    /// </summary>
    public static string Description =>
        "Textual content. Kind is set from the file extension (md, txt, csv, html, ...). "
        + "Kind is a hint by default; strict is a no-op for text (plain vs markdown is "
        + "not detectable from content).";
    // No static Kinds — text's kind is open (derived from extension at build).

    public string Value { get; }
    public override object? ToRaw() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(Value);

    public @this(string value) { Value = value ?? string.Empty; }

    // Both directions are lossless, so the wrapper owns its conversions and call sites
    // stay clean: `.Ok("x")` constructs, `string s = t.Value` reads. The explicit ==/!=
    // overloads disambiguate `t == "x"` (otherwise ambiguous between the two implicits).
    // Null-tolerant: an absent text-typed Data has a null wrapper, and consumers
    // read `someText.Value` expecting the old `string?` null-through behaviour.
    public static implicit operator string?(@this? t) => t?.Value;
    public static implicit operator @this(string s) => new(s);

    // Only the @this==@this overload — NOT a string overload. string is a reference
    // type, so a string overload would make `text == null` ambiguous (null fits both).
    // `text == "literal"` is written as `text.Value == "literal"` (to-string implicit).
    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    public override string ToString() => Value;

    /// <summary>The CLR exit door — text hands its own backing string; the
    /// shared converter (strict, loud on junk) carries it to the target.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    // ---- Ops (the behavioral targets of the `is string` sweep) ----

    /// <summary>Codepoint (Unicode scalar) count — surrogate pairs count once.
    /// Returns the PLang <c>number</c> (the public surface answers in PLang values).</summary>
    public global::app.type.number.@this Length
    {
        get
        {
            int count = 0;
            foreach (var _ in Value.EnumerateRunes()) count++;
            return count;
        }
    }

    public @this Upper() => new(Value.ToUpperInvariant());
    public @this Lower() => new(Value.ToLowerInvariant());
    public @this Trim() => new(Value.Trim());

    public bool Contains(string other) =>
        Value.Contains(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool StartsWith(string other) =>
        Value.StartsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool EndsWith(string other) =>
        Value.EndsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public int IndexOf(string other) =>
        Value.IndexOf(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);

    public @this Substring(int start, int length) => new(Value.Substring(start, length));
    public @this Replace(string oldValue, string newValue) =>
        new(Value.Replace(oldValue ?? string.Empty, newValue ?? string.Empty));

    /// <summary>Split into a native <c>list</c> of <c>text</c> values.</summary>
    public global::app.type.list.@this Split(string separator)
    {
        var list = new global::app.type.list.@this();
        var parts = string.IsNullOrEmpty(separator)
            ? new[] { Value }
            : Value.Split(separator);
        foreach (var part in parts)
            list.Add(new global::app.data.@this("", new @this(part)));
        return list;
    }

    // ---- Truthiness (item) ----

    /// <summary>Empty text is falsy; any non-empty text is truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Specificity floor — every other ranked type outranks text and drives.</summary>
    internal static int CompareRank => 10;

    /// <summary>
    /// Ordinal, case-insensitive ordering in caller order. Text is the floor type,
    /// so it only drives a pair the other side couldn't claim; coercion is the
    /// plain string form of the leaf.
    /// </summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        // Text coerces the other side into its kind: the wrapper's content, a raw
        // string, an enum's NAME (`where Status equals 'Timeout'`), or a domain
        // value's canonical text form (its ToString — e.g. an Ask renders its
        // answer for exactly this comparison, a type entity its name). Containers
        // (dict/list) have no honest text form — they stay non-coercible, so
        // `%dict% == "text"` is Incomparable, not a serialization comparison.
        static string? Coerce(object? v) => v switch
        {
            @this t => t.Value,
            string s => s,
            System.Enum e => e.ToString(),
            global::app.type.dict.@this or global::app.type.list.@this => null,
            System.Collections.IDictionary or System.Collections.IList => null,
            null => null,
            _ => v.ToString(),
        };
        var sa = Coerce(a);
        var sb = Coerce(b);
        if (sa == null || sb == null) return global::app.data.Comparison.Incomparable;
        var c = string.Compare(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    // ---- Equality + order (ordinal, case-insensitive — see class doc) ----

    public bool AreEqual(object? other) => other switch
    {
        @this t => string.Equals(Value, t.Value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Value, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public bool Equals(@this? other) =>
        other is not null && string.Equals(Value, other.Value, System.StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
}
