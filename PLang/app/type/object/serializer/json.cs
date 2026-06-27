using System.Text.Json;

namespace app.type.@object.serializer;

/// <summary>
/// Reader for the <c>object</c> shape encoded as <c>json</c>. <c>object</c>
/// names hierarchical/tree data by shape (a dict navigated by key); <c>json</c>
/// is one encoding of it (xml/yaml are siblings, future kinds). This re-houses
/// the json-string → dictionary decode that lived inline on
/// <see cref="app.type.@this.Convert(string)"/> — the same System.Text.Json
/// pipeline, now reached through the reader registry at <c>(object, json)</c>.
///
/// <para>There is no <c>Write</c> mirror here: an <c>object</c> tree renders to
/// json through the channel serializer's own json writer (the wire layer),
/// not a per-type renderer. The reader is the type-owned half — it turns the
/// type's own raw form back into the value.</para>
/// </summary>
public static class json
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        // Content off I/O rides as binary bytes; the json is text — decode through
        // the text type (it owns bytes→string), then parse.
        if (raw is not (string or byte[])) return raw;
        string s = new global::app.type.text.@this(raw).ToString();
        if (string.IsNullOrEmpty(s)) return null;
        // Narrow the JsonElement graph to born-native items via the json entry
        // parse — leaving JsonElement values would re-serialize as their
        // reflection shape ({"valueKind":...}) and navigate awkwardly.
        var parsed = JsonSerializer.Deserialize<object?>(s, _opts);
        return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);
    }
}
