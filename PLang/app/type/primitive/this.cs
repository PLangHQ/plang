using System.Text.Json.Nodes;

namespace app.type.primitive;

/// <summary>
/// The CLR-primitive entries seeded into <c>app.type.catalog.@this</c>'s registry
/// at App init — the "no folder, no Resolve, no Build" types that still need
/// a PLang name (<c>string</c>, <c>int</c>, <c>decimal</c>, …) plus their
/// aliases (<c>text</c>, <c>integer</c>, <c>boolean</c>, …).
///
/// Owns three views:
///   <see cref="Aliases"/> — every name (including aliases) → CLR type.
///   <see cref="Canonical"/> — CLR type → canonical short PLang name.
///   <see cref="MimeMap"/>  — MIME content-type → CLR type (read by
///     <c>app.type.catalog.@this.ClrFromMime</c>, kept here so the seeded data
///     stays in one place).
///
/// Pure data, no per-App divergence — exposed as static so the no-context
/// fallback helpers on <c>app.type.catalog.@this</c> can read it without an
/// instance.
/// </summary>
public static class @this
{
    public static IReadOnlyDictionary<string, System.Type> Aliases { get; } =
        new Dictionary<string, System.Type>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = typeof(string),
            ["text"] = typeof(string),
            ["int"] = typeof(int),
            ["integer"] = typeof(int),
            ["long"] = typeof(long),
            ["float"] = typeof(float),
            ["double"] = typeof(double),
            ["decimal"] = typeof(decimal),
            ["bool"] = typeof(bool),
            ["boolean"] = typeof(bool),
            // plang-types Stage 6: temporal rebinds.
            // datetime → DateTimeOffset (DateTime banished from PLang type bindings);
            // date → DateOnly, time → TimeOnly (trivial CLR wrappers);
            // duration → TimeSpan, with timespan kept as a deprecated alias.
            ["datetime"] = typeof(System.DateTimeOffset),
            ["date"] = typeof(System.DateOnly),
            ["time"] = typeof(System.TimeOnly),
            ["duration"] = typeof(System.TimeSpan),
            ["guid"] = typeof(System.Guid),
            ["byte"] = typeof(byte),
            ["bytes"] = typeof(byte[]),
            ["list"] = typeof(List<object>),
            ["array"] = typeof(object[]),
            // dict/dictionary/map → the native object value type (collections
            // hold Data). The raw Dictionary<string,object> entry that used to
            // back these is retired; typed dictionaries still surface as the
            // generic `dict<k,v>` shape via GetTypeName, separate from this.
            ["dictionary"] = typeof(app.type.dict.@this),
            ["dict"] = typeof(app.type.dict.@this),
            ["map"] = typeof(app.type.dict.@this),
            ["object"] = typeof(object),
            ["dynamic"] = typeof(object),
            ["json"] = typeof(JsonNode),
            // Text-shaped file extensions — registered as string aliases so
            // file.read.Build()'s extension-derived Type stamp ("csv", "txt", ...)
            // doesn't surface "Unknown type" at runtime. Annotation stays specific
            // (goal.getTypes still reports "csv"); only the runtime conversion
            // target degrades to string.
            ["csv"] = typeof(string),
            ["txt"] = typeof(string),
            ["xml"] = typeof(string),
            ["yaml"] = typeof(string),
            ["yml"] = typeof(string),
            ["int?"] = typeof(int?),
            ["long?"] = typeof(long?),
            ["double?"] = typeof(double?),
            ["bool?"] = typeof(bool?),
            ["datetime?"] = typeof(System.DateTimeOffset?),
            ["guid?"] = typeof(System.Guid?),
        };

    public static IReadOnlyDictionary<System.Type, string> Canonical { get; } =
        new Dictionary<System.Type, string>
        {
            // `text` is the canonical PLang name for textual content; `string`
            // stays as an accepted alias (Aliases still has both entries → typeof(string)).
            [typeof(string)] = "text",
            // Numeric primitives surface as `number` with kind carried separately
            // — `int/long/decimal/double/float` are kinds of `number`, not
            // top-level names. The kind comes from the `number.Build` hook (for
            // literals) or the CLR numeric type (for declared returns).
            [typeof(int)] = "number",
            [typeof(long)] = "number",
            [typeof(float)] = "number",
            [typeof(double)] = "number",
            [typeof(decimal)] = "number",
            [typeof(bool)] = "bool",
            [typeof(System.DateTime)] = "datetime",   // legacy; new code targets DateTimeOffset
            [typeof(System.DateTimeOffset)] = "datetime",
            [typeof(System.DateOnly)] = "date",
            [typeof(System.TimeOnly)] = "time",
            [typeof(System.TimeSpan)] = "duration",
            [typeof(System.Guid)] = "guid",
            [typeof(byte)] = "byte",
            [typeof(byte[])] = "bytes",
            [typeof(object)] = "object",
            // Native object value type → "dict" (keeps the no-context Data.Type
            // derivation from collapsing to the @this class name "this").
            [typeof(app.type.dict.@this)] = "dict",
        };

    /// <summary>
    /// The always-on fundamental vocabulary the LLM builder sees, split by the
    /// one question that decides where a kind comes from: can the value be
    /// written inline in a goal, or only referenced?
    ///
    /// <para><b>Inline fundamentals</b> — the value can be a literal
    /// (<c>5</c> → number, <c>true</c> → bool, <c>"hi"</c> → text). The LLM tags
    /// these by looking at the written value. Their kind is the numeric
    /// precision or comes from an explicit <c>as</c> — never the literal's
    /// spelling.</para>
    /// </summary>
    public static IReadOnlyList<string> InlineFundamentals { get; } = new[]
        { "text", "number", "bool", "object", "list", "dict", "datetime", "date", "time", "duration", "guid" };

    /// <summary>
    /// <b>Reference fundamentals</b> — PLang is higher-level than C# (it is
    /// <em>for</em> files, media, web, AI), so these are fundamental language
    /// types, not library types. You can only write a path/handle to one, never
    /// the data inline — there is no image literal in a goal. Their kind is
    /// declared (<c>as image</c>) or produced by an action (<c>read</c>), parsed
    /// from the path's extension. (<c>bytes</c> is borderline — base64 is
    /// sort-of-writable — but behaves like a reference fundamental.)
    /// </summary>
    public static IReadOnlyList<string> ReferenceFundamentals { get; } = new[]
        { "image", "video", "audio", "path", "bytes" };

    /// <summary>The full fundamental set — membership test for prompt scoping.</summary>
    public static IReadOnlySet<string> Fundamentals { get; } =
        new HashSet<string>(InlineFundamentals.Concat(ReferenceFundamentals), System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Names exposed to the LLM builder catalog — the fundamental vocabulary,
    /// always-on so the LLM can tag inline literals and recognise a developer's
    /// <c>as image</c>. `text` is canonical for strings, `number` for numerics
    /// (kind carries the precision); the media/path reference fundamentals are
    /// first-class here, not buried in the format-family kinds. Domain/result
    /// types are surfaced separately via schemas; their kinds never join the
    /// always-on table.
    /// </summary>
    public static IReadOnlyList<string> BuilderNames { get; } =
        InlineFundamentals.Concat(ReferenceFundamentals).ToList();

}
