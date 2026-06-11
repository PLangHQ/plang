namespace app.type.item.serializer;

/// <summary>
/// Reader for the <c>item</c> shape encoded as <c>json</c>. Born-native names a json
/// payload of unknown shape <c>item</c> (the universal value), so a read of <c>.json</c>
/// stamps <c>{item, json}</c> and materializes through <c>(item, json)</c> here. Same
/// json-string → CLR decode as the <c>(object, json)</c> reader — delegated, one pipeline.
/// </summary>
public static partial class json
{
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
    internal static object? Parse(object? value, int depth = 0)
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
                System.Text.Json.JsonValueKind.Object => global::app.data.@this.IsDataMarked(element)
                    ? System.Text.Json.JsonSerializer.Deserialize<global::app.data.@this>(element)
                    : ObjectLeaf(element, depth),
                System.Text.Json.JsonValueKind.Array => ArrayLeaf(element, depth),
                _ => element,
            };
        }

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

    private static dict.@this ObjectLeaf(System.Text.Json.JsonElement element, int depth)
    {
        var d = new dict.@this();
        foreach (var prop in element.EnumerateObject())
            d.Set(new global::app.data.@this(prop.Name, Parse(prop.Value, depth + 1)));
        return d;
    }

    private static list.@this ArrayLeaf(System.Text.Json.JsonElement element, int depth)
    {
        var l = new list.@this();
        foreach (var item in element.EnumerateArray())
        {
            // A marked element reconstructs as a Data — the list ROW holds it
            // directly (rows are the legitimate Data containers).
            var parsed = Parse(item, depth + 1);
            l.Add(parsed as global::app.data.@this ?? new global::app.data.@this("", parsed));
        }
        return l;
    }

    // A %var% reference is an UNRESOLVED reference, not yet a typed value —
    // it stays a raw string for the resolution path. Only a literal with no
    // %ref% pattern is born native as text.
    private static object TextLeaf(string s)
    {
        // if/return, NOT a ternary: the common type of (string, text.@this)
        // would silently convert the wrapper back via text's implicit operator.
        if (s.IndexOf('%') >= 0 && RefRegex().IsMatch(s)) return s;
        return new text.@this(s);
    }

    [System.Text.RegularExpressions.GeneratedRegex("%[^%]+%")]
    private static partial System.Text.RegularExpressions.Regex RefRegex();

    private static object NumberLeaf(System.Text.Json.JsonElement element)
    {
        if (element.TryGetInt64(out var l)) return number.@this.From(l);
        // Bare decimal-point literal → double by default (decimal is opt-in
        // via `as number/decimal`), matching universal language convention.
        return number.@this.From(element.GetDouble());
    }
}
