namespace app.type.item.serializer;

/// <summary>
/// Reader for the <c>item</c> shape encoded as <c>json</c>. Born-native names a json
/// payload of unknown shape <c>item</c> (the universal value), so a read of <c>.json</c>
/// stamps <c>{item, json}</c> and materializes through <c>(item, json)</c> here.
/// </summary>
public partial class json
{
    // The parser is born with the context it births values from — a parsed native
    // dict/list carries this context, so its entries/elements are born-with-context
    // when read. No threading: the context rides the parser, set once at construction.
    private readonly actor.context.@this _context;

    public json(actor.context.@this context) => _context = context;

    private const int MaxDepth = 128;

    /// <summary>
    /// Reads ONE value off an <see cref="app.type.format.IReader"/> — the open
    /// <c>item</c> slot's content. A value is never a Data: a root object is a dict and a root
    /// array a list, whatever their shape (the typed-entry rule belongs to a container's
    /// entries). A scalar streams directly off the pass — no DOM. Cursor lands on the value's
    /// last token, per the reader contract.
    /// </summary>
    internal object? Read<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
        => reader.Peek() switch
        {
            global::app.type.format.TokenKind.Null => null,
            global::app.type.format.TokenKind.Bool => reader.Bool(),
            global::app.type.format.TokenKind.Number => reader.Number(),
            global::app.type.format.TokenKind.String => StringSlot(reader.String(), ctx),
            _ => ParseRaw(reader.RawValue(), ctx),
        };

    /// <summary>
    /// Reads ONE container entry off an <see cref="app.type.format.IReader"/> into a raw
    /// slot (store raw, type on read) — a dict's / list's / snapshot's entries. An entry that is a
    /// typed value (<c>{type:{name,…}, value:…}</c>) or a <c>@schema:data</c> element IS a Data;
    /// any other object/array is a native container. A scalar streams directly off the pass — no
    /// DOM. Cursor lands on the value's last token, per the reader contract.
    /// </summary>
    internal object? Entry<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
        => reader.Peek() switch
        {
            global::app.type.format.TokenKind.Null => null,
            global::app.type.format.TokenKind.Bool => reader.Bool(),
            global::app.type.format.TokenKind.Number => reader.Number(),
            global::app.type.format.TokenKind.String => StringSlot(reader.String(), ctx),
            _ => RawEntry(reader.RawValue(), ctx),
        };

    private object? RawEntry(byte[] utf8, global::app.type.reader.ReadContext? ctx)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(utf8);
        return RawSlot(doc.RootElement, ctx, 0);
    }

    // A %ref% string slot in an authored container rides as a stamped text item (so the
    // template survives the container's fresh-per-read); a literal slot stays a raw scalar
    // (flagging it would change its canonicalization/signing). A runtime-ingest slot is
    // never a template (injection-safe). The text takes the row's variables it holds.
    private static object? StringSlot(string s, global::app.type.reader.ReadContext? ctx)
        => ctx?.Template != null && new text.@this(s, ctx.Template, ctx.Variable) is { HasVariable: true } held
            ? held
            : s;

    // A typed value WITHOUT the @schema layer marker — a dict/list entry's {type:{name,…}, value:…}
    // shape (a goal-call argument row, a config entry). The container knows its entries are typed
    // values, so this rides as a Data (the data reader reads type+value), no @schema needed.
    // Distinguished from a plain object by a structured `type` + a `value` sibling — a user object
    // literally shaped {type:{name:…}, value:…} is the accepted rare collision (entries carry type,
    // not @schema, by design). Both the eager (Parse) and lazy (RawSlot) paths honour it — a nested
    // typed value materialises as a Data either way.
    private static bool IsTypedEntry(System.Text.Json.JsonElement element)
        => element.TryGetProperty("type", out var t)
           && t.ValueKind == System.Text.Json.JsonValueKind.Object
           && t.TryGetProperty("name", out _)
           && element.TryGetProperty("value", out _);

    /// <summary>The container entry <paramref name="utf8"/> is, when it is a typed value (a Data row a
    /// list or dict wrote: <c>{type:{name,…}, value:…}</c>, or <c>@schema:data</c>) — read as that Data, as
    /// every nested entry reads; null for any other value, which the container's element type reads. A row
    /// nested in the build's own bytes (<paramref name="ctx"/>) is the build's too.</summary>
    internal global::app.data.@this? Typed(byte[] utf8, global::app.type.reader.ReadContext ctx)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(utf8);
        var element = doc.RootElement;
        return element.ValueKind == System.Text.Json.JsonValueKind.Object
               && (global::app.data.@this.IsDataMarked(element) || IsTypedEntry(element))
            ? new global::app.data.reader.@this().Read(utf8, new global::app.type.reader.ReadContext(_context, Verify: false, IsBuilt: ctx.IsBuilt))
            : null;
    }

    private object? ParseRaw(byte[] utf8, global::app.type.reader.ReadContext? ctx)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(utf8);
        return Parse(doc.RootElement, ctx);
    }

    /// <summary>
    /// THE json value parse — a deserialized System.Text.Json graph narrows to born-native items,
    /// once, at the parse leaf: every scalar leaf is its wrapper (string→text unless it carries a
    /// %ref%, number→number with the exact tower, true/false→bool, null→the null VALUE singleton);
    /// an object is a native dict, an array a native list. A value is never a Data — a typed /
    /// <c>@schema:data</c> element reconstructs as a Data only as a container's entry. This lives
    /// with the json reader — the parse belongs to the format, not to Data.
    /// </summary>
    internal object? Parse(object? value, global::app.type.reader.ReadContext? ctx = null, int depth = 0)
    {
        if (depth > MaxDepth)
            throw new System.InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxDepth})");

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => TextLeaf(element.GetString() ?? "", ctx),
                System.Text.Json.JsonValueKind.Number => number.@this.Parse(element.GetRawText())!,
                System.Text.Json.JsonValueKind.True => new @bool.@this(true),
                System.Text.Json.JsonValueKind.False => new @bool.@this(false),
                System.Text.Json.JsonValueKind.Null => @null.@this.Instance,
                System.Text.Json.JsonValueKind.Undefined => @null.@this.Instance,
                // A value object is a dict, whatever its shape — a Data rides only as a
                // container's entry (RawSlot, reached through the leaves for the children).
                System.Text.Json.JsonValueKind.Object => ObjectLeaf(element, ctx, depth),
                System.Text.Json.JsonValueKind.Array => ArrayLeaf(element, ctx, depth),
                _ => element,
            };
        }


        // System.Text.Json.Nodes DOM types (JsonObject/JsonArray/JsonValue)
        // round-trip through the JsonElement path so numeric/null/bool
        // semantics stay identical, with no duplicated walkers.
        if (value is System.Text.Json.Nodes.JsonNode jsonNode)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonNode.ToJsonString());
            return Parse(doc.RootElement, ctx, depth);
        }

        return value;
    }

    // Store raw, type on read: a container holds its leaves as RAW CLR (scalar)
    // or a native sub-container — never a Data per element at rest. An element
    // types itself when something reads it (the container's normalize-on-read).
    // A `@schema:data`-marked element is the one place a Data rides — it carries
    // its own type/signature, so it reconstructs as a Data straight into the slot.
    private dict.@this ObjectLeaf(System.Text.Json.JsonElement element, global::app.type.reader.ReadContext? ctx, int depth)
    {
        // An authored container is born with its template mode so its `%ref%` leaves re-resolve
        // on read (dict.@this.Value → Resolve); a runtime-ingest read (ctx null) stays literal.
        var d = new dict.@this { Template = ctx?.Template };
        foreach (var prop in element.EnumerateObject())
            d.Set(prop.Name, RawSlot(prop.Value, ctx, depth + 1));
        return d;
    }

    private list.@this ArrayLeaf(System.Text.Json.JsonElement element, global::app.type.reader.ReadContext? ctx, int depth)
    {
        var l = new list.@this { Template = ctx?.Template };
        foreach (var item in element.EnumerateArray())
            l.AddRaw(RawSlot(item, ctx, depth + 1));
        return l;
    }

    // One container slot from a json token — raw scalar, native sub-container
    // (itself lazy), or a reconstructed Data for a marked element.
    private object? RawSlot(System.Text.Json.JsonElement element, global::app.type.reader.ReadContext? ctx, int depth)
    {
        if (depth > MaxDepth)
            throw new System.InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxDepth})");
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => StringSlot(element.GetString() ?? "", ctx),
            // a number: its raw text, read by number's one rule
            System.Text.Json.JsonValueKind.Number => number.@this.Parse(element.GetRawText()),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Undefined => null,
            System.Text.Json.JsonValueKind.Object => global::app.data.@this.IsDataMarked(element) || IsTypedEntry(element)
                ? new global::app.data.reader.@this().Read(System.Text.Encoding.UTF8.GetBytes(element.GetRawText()),
                    new global::app.type.reader.ReadContext(_context, Verify: false, IsBuilt: ctx?.IsBuilt ?? false))
                : ObjectLeaf(element, ctx, depth),
            System.Text.Json.JsonValueKind.Array => ArrayLeaf(element, ctx, depth),
            _ => element.GetRawText(),
        };
    }

    // A %var% reference is an UNRESOLVED reference, not yet a typed value. In an authored
    // container (ctx.Template set) it rides as a STAMPED text template so it re-resolves on
    // read; a runtime-ingest slot (ctx null) stays a raw string (injection-safe, and a bare
    // literal keeps its canonicalization). Only a plain literal with no %ref% is born native text.
    private static object TextLeaf(string s, global::app.type.reader.ReadContext? ctx)
    {
        // if/return, NOT a ternary: the common type of (string, text.@this)
        // would silently convert the wrapper back via text's implicit operator.
        if (ctx?.Template != null)
        {
            var held = new text.@this(s, ctx.Template, ctx.Variable);
            if (held.HasVariable) return held;
        }
        else if (s.Contains('%') && new global::app.type.item.variable.parser.@this(s).Variable.Count > 0)
            return s;
        return new text.@this(s);
    }
}
