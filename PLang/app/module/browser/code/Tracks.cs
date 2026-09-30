using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using app.module.screen.code.media;

namespace app.module.browser.code;

/// <summary>
/// The videos pages play that the host shows (video redirection): one track per video buffer a page's
/// MediaSource made (<see cref="Media"/> gives the page its stand-in). A track reads what the page
/// appends (WebM, MP4), answers with what is buffered, and passes its coded frames to the host — which
/// decodes them — with where on the screen and at what time the video is.
/// Host messages (screen message 8): {track,op:"frames",codec,width,height,frames:[[time,duration,key,n],…]}
/// + the frames' bytes; {track,op:"state",rect:[x,y,w,h],time,paused,rate,seeking,shown};
/// {track,op:"remove",start,end}; {track,op:"gone"}.
/// </summary>
internal sealed class Tracks(Browser browser)
{
    private readonly ConcurrentDictionary<(string session, string buffer), Track> tracks = new();
    private static int numbers;

    /// <summary>What a page's script said (<paramref name="payload"/>), from DevTools' page
    /// <paramref name="target"/> (<paramref name="session"/>); <paramref name="reply"/> answers it.</summary>
    internal async Task Heard(string session, string target, string payload, Func<string, Task> reply)
    {
        if (browser.Screen?.Display is not { } display) return;
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException) { return; }
        using var _ = document;
        var m = document.RootElement;
        if (m.ValueKind != JsonValueKind.Object) return;
        var buffer = m.TryGetProperty("buffer", out var b) ? b.GetString() ?? "" : "";
        var key = (session, buffer);
        var op = m.TryGetProperty("op", out var o) ? o.GetString() : null;
        if (op == "add")
        {
            if (Container.For(m.TryGetProperty("mime", out var mime) ? mime.GetString() ?? "" : "") is not { } container) return;
            if (tracks.TryRemove(key, out var old)) old.Gone(display);
            tracks[key] = new Track(Interlocked.Increment(ref numbers), container);
            return;
        }
        if (!tracks.TryGetValue(key, out var track))
        {
            // unknown (gone already): still answered — the page waits for every append and remove
            if (op is "append" or "remove") await reply(new JsonObject { ["id"] = Int(m, "id"), ["buffered"] = new JsonArray() }.ToJsonString());
            return;
        }
        switch (op)
        {
            case "append":
                // the bytes straight from the JSON (no string of them first); the frames are slices of them
                if (m.TryGetProperty("data", out var data) && data.TryGetBytesFromBase64(out var bytes)) track.Append(bytes, display);
                await reply(track.Answer(Int(m, "id")));
                break;
            case "remove":
                track.Remove(Number(m, "start"), Number(m, "end"), display);
                await reply(track.Answer(Int(m, "id")));
                break;
            case "abort": track.Container.Reset(); break;
            case "offset": track.Container.Offset = Number(m, "value"); break;
            case "state":
                if (browser.Windows.IdOf(target) is { } window && display.Origin((int)window) is { } origin)
                    track.State(m, origin.X, origin.Y, display);
                break;
            case "gone":
                if (tracks.TryRemove(key, out var gone)) gone.Gone(display);
                break;
        }
    }

    /// <summary>A page went away (or navigated): its tracks go.</summary>
    internal void Left(string session)
    {
        if (browser.Screen?.Display is not { } display) return;
        foreach (var key in tracks.Keys.Where(k => k.session == session))
            if (tracks.TryRemove(key, out var track)) track.Gone(display);
    }

    private static int Int(JsonElement m, string name) => (int)Number(m, name);
    private static double Number(JsonElement m, string name)
        => m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}

/// <summary>One redirected video buffer: its container, the times of the frames it holds (what is
/// buffered), and its number for the host.</summary>
internal sealed class Track(int number, Container container)
{
    private readonly List<(double time, double duration, bool key)> held = new();
    private readonly Lock gate = new();

    internal Container Container { get; } = container;

    /// <summary>Appended bytes: the frames whole now go to the host.</summary>
    internal void Append(ReadOnlyMemory<byte> bytes, screen.code.wayland.Display display)
    {
        List<Coded> frames;
        lock (gate)
        {
            frames = Container.Take(bytes);
            foreach (var f in frames) held.Add((f.Time, f.Duration, f.Key));
            held.Sort((a, b) => a.time.CompareTo(b.time));
        }
        if (frames.Count == 0) return;
        var list = new JsonArray();
        foreach (var f in frames) list.Add(new JsonArray(f.Time, f.Duration, f.Key, f.Bytes.Length));
        display.Media(new JsonObject
        {
            ["track"] = number, ["op"] = "frames", ["codec"] = Container.Codec,
            ["width"] = Container.Width, ["height"] = Container.Height, ["frames"] = list,
        }, frames.ConvertAll(f => f.Bytes));
    }

    /// <summary>MediaSource's remove: the frames from <paramref name="start"/> to <paramref name="end"/>
    /// go, and those after them up to the next key frame (nothing can decode them now).</summary>
    internal void Remove(double start, double end, screen.code.wayland.Display display)
    {
        lock (gate)
        {
            var next = held.FindIndex(f => f.time >= end && f.key);
            var until = next < 0 ? double.MaxValue : held[next].time;
            held.RemoveAll(f => f.time >= start && f.time < Math.Max(end, until));
            end = Math.Max(end, until);
        }
        display.Media(new JsonObject { ["track"] = number, ["op"] = "remove", ["start"] = start, ["end"] = end });
    }

    /// <summary>The answer to an append or remove: what is buffered now, in time ranges (a gap of more
    /// than two frames splits them).</summary>
    internal string Answer(int id)
    {
        var ranges = new JsonArray();
        lock (gate)
        {
            double? from = null, to = null;
            foreach (var (time, duration, _) in held)
            {
                if (from != null && time > to + Math.Max(0.1, 2 * duration))
                {
                    ranges.Add(new JsonArray(from.Value, to!.Value));
                    from = null;
                }
                from ??= time;
                to = Math.Max(to ?? time, time + duration);
            }
            if (from != null) ranges.Add(new JsonArray(from.Value, to!.Value));
        }
        return new JsonObject { ["id"] = id, ["buffered"] = ranges }.ToJsonString();
    }

    /// <summary>Where the video is (its element, in the page, moved to the screen by the page's
    /// origin) and its clock, for the host.</summary>
    internal void State(JsonElement m, int x, int y, screen.code.wayland.Display display)
    {
        JsonNode? Copy(string name) => m.TryGetProperty(name, out var v) ? JsonNode.Parse(v.GetRawText()) : null;
        var head = new JsonObject { ["track"] = number, ["op"] = "state" };
        if (m.TryGetProperty("rect", out var r) && r.ValueKind == JsonValueKind.Array && r.GetArrayLength() == 4)
            head["rect"] = new JsonArray(r[0].GetInt32() + x, r[1].GetInt32() + y, r[2].GetInt32(), r[3].GetInt32());
        foreach (var name in new[] { "time", "paused", "rate", "seeking", "shown" }) head[name] = Copy(name);
        display.Media(head);
    }

    /// <summary>It ends: the host lets its frames and decoder go.</summary>
    internal void Gone(screen.code.wayland.Display display)
        => display.Media(new JsonObject { ["track"] = number, ["op"] = "gone" });
}
