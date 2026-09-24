using System.Text.Json.Nodes;

namespace app.type.primitive;

/// <summary>
/// The primitives' spelled names — the type registry's own data (<c>app.Type.Primitive</c>).
///   <see cref="Aliases"/> — every spelled name (<c>string</c>, <c>int</c>, <c>boolean</c>, …) → the
///     C# type it spells; the registry resolves it to the item that owns that C# type.
///   <see cref="Canonical"/> — a C# type → the canonical plang name of the item that owns it.
/// </summary>
public sealed class @this
{
    public IReadOnlyDictionary<string, System.Type> Aliases { get; } =
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
            // list/array → the native list value type (collections hold Data). A typed list
            // names itself {list, kind: element} through the entity door.
            ["list"] = typeof(app.type.item.list.@this),
            ["array"] = typeof(app.type.item.list.@this),
            // tag → the native tag value type (a normalized, case-insensitive label).
            ["tag"] = typeof(app.type.item.tag.@this),
            // dict/dictionary/map → the native object value type (collections hold Data). A
            // typed dictionary names itself {dict, kind: value} through the entity door.
            ["dictionary"] = typeof(app.type.item.dict.@this),
            ["dict"] = typeof(app.type.item.dict.@this),
            ["map"] = typeof(app.type.item.dict.@this),
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

    public IReadOnlyDictionary<System.Type, string> Canonical { get; } =
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
            // Native object value type → "dict" (keeps the no-context Data.Type
            // derivation from collapsing to the @this class name "this").
            [typeof(app.type.item.dict.@this)] = "dict",
            // Native list value type → "list" (same no-context derivation reason).
            [typeof(app.type.item.list.@this)] = "list",
            [typeof(app.type.item.tag.@this)] = "tag",
        };

}
