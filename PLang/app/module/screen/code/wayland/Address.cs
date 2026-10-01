using System.Text.Json.Nodes;

namespace app.module.screen.code.wayland;

/// <summary>
/// Where a window is. <see cref="Path"/> is what the globe shows — plang's path for plang's own
/// (<c>/Desktop/Start.goal</c>, <c>/os/…</c>), a website's address as it is; <see cref="Source"/> is where
/// that really is (<c>file:///home/plang/Desktop/Start.goal</c>, the page's url). An address copies itself.
/// </summary>
internal sealed record Address(string Path, string Source)
{
    internal static readonly Address None = new("", "");

    /// <summary>The address a command gives: <c>{"path", "source"}</c>, or one text that is both.</summary>
    internal static Address Of(JsonNode? given)
    {
        if (given is JsonObject o)
        {
            string? S(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            var path = S("path") ?? S("source") ?? "";
            return new Address(path, S("source") ?? path);
        }
        return given is JsonValue value && value.TryGetValue<string>(out var text) ? new Address(text, text) : None;
    }

    /// <summary>What a copy of it is, on the clipboard. For now its path as text; once the clipboard holds
    /// plang's values, a copy (type "copy") that knows where it came from (todo).</summary>
    internal string Copy() => Path;
}
