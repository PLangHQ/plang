using System.Text.Json.Nodes;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// Where a window is. <see cref="Path"/> is what the globe shows — plang's path for plang's own
/// (<c>/Desktop/Start.goal</c>, <c>/os/…</c>), a website's address as it is; <see cref="Source"/> is where
/// that really is (<c>file:///home/plang/Desktop/Start.goal</c>, the page's url); <see cref="Origin"/> is
/// where the address came from (the page that named it, the goal and .pr it passed through). An address
/// copies itself.
/// </summary>
internal sealed record Address(string Path, string Source, JsonObject? Origin = null)
{
    internal static readonly Address None = new("", "");

    /// <summary>The address a command gives: <c>{"path", "source", "origin"}</c>, or one text that is both.</summary>
    internal static Address Of(JsonNode? given)
    {
        if (given is JsonObject o)
        {
            string? S(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            var path = S("path") ?? S("source") ?? "";
            return new Address(path, S("source") ?? path, o["origin"]?.DeepClone() as JsonObject);
        }
        return given is JsonValue value && value.TryGetValue<string>(out var text) ? new Address(text, text) : None;
    }

    /// <summary>A copy of it: its path as text, and as a value — <c>{"type": "copy", "path", "source",
    /// "from": {where it came from, the window}, "at"}</c> — which a paste with Shift held gets.</summary>
    internal (string Text, string Value) Copy(string window)
    {
        var from = Origin?.DeepClone() as JsonObject ?? new JsonObject();
        from["window"] = window;
        var value = new JsonObject
        {
            ["type"] = "copy",
            ["path"] = Path,
            ["source"] = Source,
            ["from"] = from,
            ["at"] = DateTimeOffset.UtcNow.ToString("o"),
        };
        // written for a person to read: characters as they are (— ´ +), not \u escapes
        return (Path, value.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
    }
}
