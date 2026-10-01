using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Browser = app.module.browser.Browser;

namespace app.module.window;

/// <summary>
/// A browser's windows on the screen: the desktop, and every window a page opened in. The screen
/// tells about each window (its id, when it gets a title, when it closes); a window is paired with
/// its page when it gets a title — the unpaired page with that title, else the newest unpaired one —
/// and is then shown: a window <c>window.open</c> is waiting for (the oldest), or a new one. A page
/// of the app's own gets <c>plang(text)</c>.
/// </summary>
internal sealed class Windows(Browser browser)
{
    private static readonly HttpClient DevTools = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly ConcurrentDictionary<long, Window> shown = new();
    private readonly ConcurrentQueue<Window> opening = new();
    private readonly HashSet<string> looked = new();
    private readonly Lock gate = new();

    /// <summary>The desktop: the first page, the whole screen, id 0.</summary>
    internal Window Desktop { get; } = new();

    internal Window? ById(long id) => id == Desktop.Number ? Desktop : shown.GetValueOrDefault(id);

    /// <summary>A window about to open: shown when its page is.</summary>
    internal Window Opening()
    {
        var window = new Window();
        opening.Enqueue(window);
        return window;
    }

    /// <summary>The desktop's page (<paramref name="target"/>), shown as window 0.</summary>
    internal Task ShowDesktop(string target, string address)
    {
        Desktop.Address = address;
        return Desktop.Show(0, new Page(target, browser.Port, browser.Own), browser.Own(address) ? browser.Message : null);
    }

    /// <summary>True the first time <paramref name="target"/> is asked about.</summary>
    internal bool First(string target)
    {
        lock (gate) return looked.Add(target);
    }

    /// <summary>What the screen says about a window: titled — paired with its page and shown (or its
    /// title and address follow); closed — it goes.</summary>
    internal async Task Follow(JsonObject e)
    {
        if (e["window"]?.GetValue<string>() is not { } what || e["id"] is not JsonValue idValue) return;
        if (!idValue.TryGetValue<int>(out var id)) return;   // the screen's ids are ints
        if (what == "closed")
        {
            if (shown.TryRemove(id, out var gone)) gone.Gone();
            return;
        }
        if (what != "titled") return;
        var title = e["title"]?.GetValue<string>() ?? "";
        try
        {
            using var list = JsonDocument.Parse(await DevTools.GetStringAsync($"http://127.0.0.1:{browser.Port}/json/list"));
            if (shown.TryGetValue(id, out var known))
            {
                // gone somewhere: its title and address follow
                known.Named = title;
                if (Of(known, list.RootElement) is { } now) known.Address = now;
                browser.Screen?.Display?.Url((int)id, known.Address);
                return;
            }
            if (Free(title, list.RootElement) is not { } page) return;
            var window = opening.TryDequeue(out var waited) ? waited : new Window();
            window.Named = title;
            window.Address = page.GetProperty("url").GetString() ?? "";
            if (!shown.TryAdd(id, window)) return;
            browser.Screen?.Display?.Url((int)id, window.Address);
            await window.Show(id, new Page(page.GetProperty("id").GetString()!, browser.Port, browser.Own), browser.Own(window.Address) ? browser.Message : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
            or System.Net.WebSockets.WebSocketException or TimeoutException)
        {
            // the window stays without its page (no plang(), its goals not callable): say why
            if (browser.Context is { } context)
                await context.App.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteAsync(context.Error(
                    new global::app.error.ServiceError($"Window {id} ('{title}') couldn't be paired with its page: {ex.Message}", "WindowNotPaired", 500) { Exception = ex }));
        }
    }

    /// <summary>The address of <paramref name="window"/>'s page now, in DevTools' list.</summary>
    private static string? Of(Window window, JsonElement list)
        => list.EnumerateArray().Where(t => t.GetProperty("id").GetString() == window.Target)
            .Select(t => t.GetProperty("url").GetString()).FirstOrDefault();

    /// <summary>The page a newly titled window shows: unpaired, with that title if one has it, else
    /// the newest (DevTools lists newest first). Never the desktop's.</summary>
    private JsonElement? Free(string title, JsonElement list)
    {
        lock (gate)
        {
            var taken = shown.Values.Select(w => w.Target).Append(Desktop.Target).ToHashSet();
            var free = list.EnumerateArray()
                .Where(t => t.GetProperty("type").GetString() == "page" && !taken.Contains(t.GetProperty("id").GetString()))
                .ToList();
            return free.Where(p => p.GetProperty("title").GetString() == title).Select(p => (JsonElement?)p).FirstOrDefault()
                ?? free.Select(p => (JsonElement?)p).FirstOrDefault();
        }
    }
}
