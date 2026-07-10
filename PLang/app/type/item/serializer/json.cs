namespace app.type.item.serializer;

/// <summary>
/// Reader for the <c>item</c> shape encoded as <c>json</c>. Born-native names a json
/// payload of unknown shape <c>item</c> (the universal value), so a read of <c>.json</c>
/// stamps <c>{item, json}</c> and materializes through <c>(item, json)</c> here. Same
/// json-string → CLR decode as the <c>(object, json)</c> reader — delegated, one pipeline.
/// </summary>
public partial class json
{
    // The parser is born with the context it births values from — a parsed native
    // dict/list carries this context, so its entries/elements are born-with-context
    // when read. No threading: the context rides the parser, set once at construction.
    private readonly actor.context.@this _context;

    public json(actor.context.@this context) => _context = context;

    // Read options carrying a CONTEXT-FUL Wire for a nested Data — Options.Read adds the
    // path converter but not the Wire, so without this a nested Deserialize<Data> falls to
    // the [JsonConverter] default (a context-less Wire). Built once per parser.
    private System.Text.Json.JsonSerializerOptions? _nestedOptions;
    private System.Text.Json.JsonSerializerOptions NestedOptions()
    {
        if (_nestedOptions != null) return _nestedOptions;
        // Nested reconstruction: skip verify — the inner Data is covered by the outer signature.
        return _nestedOptions = global::app.data.Wire.ReadOptions(
            new global::app.type.reader.ReadContext(_context, Verify: false));
    }

    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.@object.serializer.json.Read(raw, kind, ctx);

    private const int MaxDepth = 128;

    /// <summary>
    /// THE json entry parse — a deserialized System.Text.Json graph narrows to
    /// born-native items, once, at the parse leaf: every scalar leaf is its
    /// wrapper (string→text unless it carries a %ref%, number→number with the
    /// exact tower, true/false→bool, null→the null VALUE singleton); an object
    /// is a native dict, an array a native list (collections hold Data end to
    /// end); a <c>@schema:data</c>-marked object reconstructs as the Data it
    /// is. This lives with the json reader — the parse belongs to the format,
    /// not to Data.
    /// </summary>
    /// <summary>
    /// Streaming sibling of <see cref="RawSlot"/> — reads ONE value off an
    /// <see cref="app.channel.serializer.IReader"/> into a raw container slot
    /// (store raw, type on read). A scalar streams directly off the pass — no DOM;
    /// a nested container / <c>@schema:data</c> element reuses the proven DOM narrow
    /// via <c>RawValue()</c> (the structured minority). Cursor lands on the value's
    /// last token, per the reader contract.
    /// </summary>
    internal object? ReadSlot<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Peek() switch
        {
            global::app.channel.serializer.TokenKind.Null => null,
            global::app.channel.serializer.TokenKind.Bool => reader.Bool(),
            global::app.channel.serializer.TokenKind.Number => reader.Number(),
            global::app.channel.serializer.TokenKind.String => StringSlot(reader.String(), ctx),
            // Array / Object — a nested container or a @schema:data Data. Capture the
            // encoded value and narrow through the same parser the eager path uses.
            _ => ParseRaw(reader.RawValue()),
        };

    // A %ref% string slot in an authored container rides as a stamped text item (so the
    // template survives the container's fresh-per-read); a literal slot stays a raw scalar
    // (flagging it would change its canonicalization/signing). A runtime-ingest slot is
    // never a template (injection-safe). The per-slot HasVariable here goes once the builder
    // stamps inside containers — Documentation/v0.2/todos.md 2026-07-01.
    private static object? StringSlot(string s, global::app.type.reader.ReadContext ctx)
        => ctx.Template != null && global::app.type.item.text.@this.HasVariable(s)
            ? new text.@this(s, ctx.Template)
            : s;

    private object? ParseRaw(byte[] utf8)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(utf8);
        return Parse(doc.RootElement);
    }

    internal object? Parse(object? value, int depth = 0)
    {
        if (depth > MaxDepth)
            throw new System.InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxDepth})");

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => TextLeaf(element.GetString() ?? ""),
                System.Text.Json.JsonValueKind.Number => NumberLeaf(element),
                System.Text.Json.JsonValueKind.True => new @bool.@this(true),
                System.Text.Json.JsonValueKind.False => new @bool.@this(false),
                System.Text.Json.JsonValueKind.Null => @null.@this.Instance,
                System.Text.Json.JsonValueKind.Undefined => @null.@this.Instance,
                System.Text.Json.JsonValueKind.Object => global::app.data.@this.IsDataMarked(element) || IsTypedEntry(element)
                    // A nested Data reads through a CONTEXT-FUL Wire (the parser is born with
                    // its context) — never the bare [JsonConverter] default, which is a
                    // context-less Wire and leaves the nested read unable to reach App.
                    ? System.Text.Json.JsonSerializer.Deserialize<global::app.data.@this>(element, NestedOptions())
                    : ObjectLeaf(element, depth),
                System.Text.Json.JsonValueKind.Array => ArrayLeaf(element, depth),
                _ => element,
            };
        }

        // A typed value WITHOUT the @schema layer marker — a dict/list entry's
        // {type:{name,…}, value:…} shape. The container knows its entries are typed
        // values, so this rides as a Data (Wire.ReadBody reads type+value), no @schema
        // needed. Distinguished from a plain object by a structured `type` + a `value`
        // sibling — a user object literally shaped {type:{name:…}, value:…} is the
        // accepted rare collision (entries carry type, not @schema, by design).
        static bool IsTypedEntry(System.Text.Json.JsonElement element)
            => element.TryGetProperty("type", out var t)
               && t.ValueKind == System.Text.Json.JsonValueKind.Object
               && t.TryGetProperty("name", out _)
               && element.TryGetProperty("value", out _);

        // System.Text.Json.Nodes DOM types (JsonObject/JsonArray/JsonValue)
        // round-trip through the JsonElement path so numeric/null/bool
        // semantics stay identical, with no duplicated walkers.
        if (value is System.Text.Json.Nodes.JsonNode jsonNode)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonNode.ToJsonString());
            return Parse(doc.RootElement, depth);
        }

        return value;
    }

    // Store raw, type on read: a container holds its leaves as RAW CLR (scalar)
    // or a native sub-container — never a Data per element at rest. An element
    // types itself when something reads it (the container's normalize-on-read).
    // A `@schema:data`-marked element is the one place a Data rides — it carries
    // its own type/signature, so it reconstructs as a Data straight into the slot.
    private dict.@this ObjectLeaf(System.Text.Json.JsonElement element, int depth)
    {
        var d = new dict.@this(_context);
        foreach (var prop in element.EnumerateObject())
            d.Set(prop.Name, RawSlot(prop.Value, depth + 1));
        return d;
    }

    private list.@this ArrayLeaf(System.Text.Json.JsonElement element, int depth)
    {
        var l = new list.@this(_context);
        foreach (var item in element.EnumerateArray())
            l.AddRaw(RawSlot(item, depth + 1));
        return l;
    }

    // One container slot from a json token — raw scalar, native sub-container
    // (itself lazy), or a reconstructed Data for a marked element.
    private object? RawSlot(System.Text.Json.JsonElement element, int depth)
    {
        if (depth > MaxDepth)
            throw new System.InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxDepth})");
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            // Cast to object so the ?: does NOT unify long and double to double
            // (a bare `long : double` ternary widens the integer to a float).
            System.Text.Json.JsonValueKind.Number =>
                element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Undefined => null,
            System.Text.Json.JsonValueKind.Object => global::app.data.@this.IsDataMarked(element)
                ? System.Text.Json.JsonSerializer.Deserialize<global::app.data.@this>(element, NestedOptions())
                : ObjectLeaf(element, depth),
            System.Text.Json.JsonValueKind.Array => ArrayLeaf(element, depth),
            _ => element.GetRawText(),
        };
    }

    // A %var% reference is an UNRESOLVED reference, not yet a typed value —
    // it stays a raw string for the resolution path. Only a literal with no
    // %ref% pattern is born native as text.
    private static object TextLeaf(string s)
    {
        // if/return, NOT a ternary: the common type of (string, text.@this)
        // would silently convert the wrapper back via text's implicit operator.
        if (global::app.type.item.text.@this.HasVariable(s)) return s;
        return new text.@this(s);
    }

    private static object NumberLeaf(System.Text.Json.JsonElement element)
    {
        if (element.TryGetInt64(out var l)) return (number.@this)l;
        // Bare decimal-point literal → double by default (decimal is opt-in
        // via `as number/decimal`), matching universal language convention.
        return (number.@this)element.GetDouble();
    }
}
