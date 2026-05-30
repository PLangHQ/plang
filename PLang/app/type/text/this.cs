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
/// No fields, no constructor signature; the LLM emits a bare string and the
/// kind is carried alongside on <c>Data</c>.</para>
/// </summary>
public sealed partial class @this
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

    public @this(string value) { Value = value ?? string.Empty; }

    public static implicit operator string(@this t) => t.Value;
    public override string ToString() => Value;
}
