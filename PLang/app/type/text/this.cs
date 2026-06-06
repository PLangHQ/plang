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
    global::app.data.IEquatableValue, global::app.data.IOrderableValue,
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

    public static implicit operator string(@this t) => t.Value;
    public override string ToString() => Value;

    // ---- Ops (the behavioral targets of the `is string` sweep) ----

    /// <summary>Codepoint (Unicode scalar) count — surrogate pairs count once.</summary>
    public int Length
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

    // ---- Equality + order (ordinal, case-insensitive — see class doc) ----

    public bool AreEqual(object? other) => other switch
    {
        @this t => string.Equals(Value, t.Value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Value, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public int Order(object? other) => other switch
    {
        @this t => string.Compare(Value, t.Value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Compare(Value, s, System.StringComparison.OrdinalIgnoreCase),
        _ => throw new global::app.data.Compare.NotOrderableException(
            $"cannot order text against {other?.GetType().Name ?? "null"}"),
    };

    public bool Equals(@this? other) =>
        other is not null && string.Equals(Value, other.Value, System.StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
}
