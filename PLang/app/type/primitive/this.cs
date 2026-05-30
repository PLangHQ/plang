using System.Text.Json.Nodes;

namespace app.type.primitive;

/// <summary>
/// The CLR-primitive entries seeded into <c>app.type.list.@this</c>'s registry
/// at App init — the "no folder, no Resolve, no Build" types that still need
/// a PLang name (<c>string</c>, <c>int</c>, <c>decimal</c>, …) plus their
/// aliases (<c>text</c>, <c>integer</c>, <c>boolean</c>, …).
///
/// Owns three views:
///   <see cref="Aliases"/> — every name (including aliases) → CLR type.
///   <see cref="Canonical"/> — CLR type → canonical short PLang name.
///   <see cref="MimeMap"/>  — MIME content-type → CLR type (read by
///     <c>app.type.list.@this.ClrFromMime</c>, kept here so the seeded data
///     stays in one place).
///
/// Pure data, no per-App divergence — exposed as static so the no-context
/// fallback helpers on <c>app.type.list.@this</c> can read it without an
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
            ["dictionary"] = typeof(Dictionary<string, object>),
            ["dict"] = typeof(Dictionary<string, object>),
            ["map"] = typeof(Dictionary<string, object>),
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
            [typeof(string)] = "string",
            [typeof(int)] = "int",
            [typeof(long)] = "long",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",
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
        };

    /// <summary>
    /// Names exposed to the LLM builder catalog — every alias-less canonical
    /// name (no <c>?</c> suffixes). Domain types are surfaced separately via
    /// schemas, not listed here.
    /// </summary>
    public static IReadOnlyList<string> BuilderNames { get; } = Aliases
        .Where(kvp => !kvp.Key.EndsWith("?"))
        .GroupBy(kvp => kvp.Value)
        .Select(g => g.First().Key)
        .ToList();
}
