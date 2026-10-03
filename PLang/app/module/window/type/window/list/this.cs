using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Browser = app.module.browser.type.browser.screen.@this;
using Window = app.module.window.type.window.@this;

namespace app.module.window.type.window.list;

/// <summary>
/// A browser's windows on the screen: the desktop, and every window a page opened in — a dict by their number on the
/// screen (<c>%browser.window[1].url%</c>, <c>%browser.window[%click.window%]%</c>; 0 is the desktop). The screen tells about
/// each window (its id, when it gets a title, when it closes); a window is paired with its page when it gets a title —
/// the unpaired page with that title, else the newest unpaired one — and is then shown: a window <c>window.open</c>
/// is waiting for (the oldest), or a new one. A page of the app's own gets <c>plang(text)</c>.
/// </summary>
public sealed class @this(Browser browser) : global::app.type.item.@this
{
    private readonly ConcurrentDictionary<long, Window> _shown = new();
    private readonly ConcurrentQueue<Window> _opening = new();
    private readonly HashSet<string> _looked = new();
    private readonly Lock _gate = new();

    /// <summary>The desktop: the first page, the whole screen, id 0.</summary>
    internal Window Desktop { get; } = new();

    /// <summary>The window numbered <paramref name="id"/> on the screen, if there is one.</summary>
    internal Window? ById(long id) => id == Desktop.Number ? Desktop : _shown.GetValueOrDefault(id);

    /// <summary>One step by index: a window's number on the screen; none there is nothing.</summary>
    public override ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
        => Get(parent, key);

    public override ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => ValueTask.FromResult(long.TryParse(key, out var number) && ById(number) is { } window
            ? new global::app.data.@this(key, window, parent: parent)
            : global::app.data.@this.NotFound(key, parent.Context));

    /// <summary>A dict of the windows by their number on the screen — the desktop (0) first, then the rest as they
    /// came: <c>foreach %browser.window%</c> gives each number and its window.</summary>
    public override IEnumerable<(global::app.data.@this key, global::app.data.@this value)> EnumerateItems(global::app.actor.context.@this? context)
    {
        if (Desktop.Number >= 0)
            yield return (new global::app.data.@this("", Desktop.Number, context: context), new global::app.data.@this("", Desktop, context: context));
        foreach (var (id, window) in _shown.OrderBy(pair => pair.Key))
            yield return (new global::app.data.@this("", id, context: context), new global::app.data.@this("", window, context: context));
    }

    /// <summary>A window about to open: shown when its page is.</summary>
    internal Window Opening()
    {
        var window = new Window();
        _opening.Enqueue(window);
        return window;
    }

    /// <summary>A window that waited to open and never will (Chromium didn't open it): no title pairs it any more.</summary>
    internal void Unopened(Window window)
    {
        lock (_gate)
        {
            var waiting = new List<Window>();
            while (_opening.TryDequeue(out var next)) if (!ReferenceEquals(next, window)) waiting.Add(next);
            foreach (var next in waiting) _opening.Enqueue(next);
        }
    }

    /// <summary>The desktop's page (<paramref name="target"/>), shown as window 0.</summary>
    internal Task ShowDesktop(string target, string address)
    {
        Desktop.Address = address;
        return Desktop.Show(0, new Page(target, browser.Cdp, browser.Own), browser.Own(address) ? browser.Message : null);
    }

    /// <summary>True the first time <paramref name="target"/> is asked about.</summary>
    internal bool First(string target)
    {
        lock (_gate) return _looked.Add(target);
    }

    /// <summary>What the screen says about a window: titled — paired with its page and shown (or its
    /// title and address follow); closed — it goes.</summary>
    internal async Task Follow(JsonObject e, global::app.actor.context.@this context)
    {
        // a window's own news names what happened ({"window":"titled","id":…}); an event that only says which window
        // it was in ({"ui":"click","window":1}) is not about the window
        if (e["window"] is not JsonValue said || !said.TryGetValue<string>(out var what) || e["id"] is not JsonValue idValue) return;
        if (!idValue.TryGetValue<int>(out var id)) return;   // the screen's ids are ints
        if (what == "closed")
        {
            if (_shown.TryRemove(id, out var gone)) gone.Gone();
            return;
        }
        if (what != "titled") return;
        var title = e["title"]?.GetValue<string>() ?? "";
        try
        {
            var pages = await browser.Cdp.Pages();
            if (_shown.TryGetValue(id, out var known))
            {
                // gone somewhere: its title and address follow — the address only when the page went
                // somewhere else, and not between plang's own pages: those name their own (Writer: the
                // file it shows, {"window":"url"})
                known.Named = title;
                if (pages.FirstOrDefault(p => p.target == known.Target) is { url: { } now } && now != known.Address)
                {
                    var within = browser.Own(now) && browser.Own(known.Address);
                    known.Address = now;
                    if (!within) browser.Display.Wayland.Url(id, Browser.AddressOf(known.Address, context));
                }
                return;
            }
            if (Free(title, pages) is not { } page) return;
            var window = _opening.TryDequeue(out var waited) ? waited : new Window();
            window.Named = title;
            window.Address = page.url;
            if (!_shown.TryAdd(id, window)) return;
            browser.Display.Wayland.Url(id, Browser.AddressOf(window.Address, context));
            await window.Show(id, new Page(page.target, browser.Cdp, browser.Own), browser.Own(window.Address) ? browser.Message : null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or TimeoutException)
        {
            // the window stays without its page (no plang(), its goals not callable): say why
            if (browser.Report is { } report)
                await report(new global::app.error.ServiceError($"Window {id} ('{title}') couldn't be paired with its page: {ex.Message}", "WindowNotPaired", 500) { Exception = ex });
        }
    }

    /// <summary>The page a newly titled window shows: unpaired, with that title if one has it, else the newest
    /// (DevTools lists them as they came). Never the desktop's.</summary>
    private (string target, string url, string title)? Free(string title, IReadOnlyList<(string target, string url, string title)> pages)
    {
        lock (_gate)
        {
            var taken = _shown.Values.Select(w => w.Target).Append(Desktop.Target).ToHashSet();
            var free = pages.Where(p => !taken.Contains(p.target)).ToList();
            return free.Where(p => p.title == title).Select(p => ((string, string, string)?)p).FirstOrDefault()
                ?? free.Select(p => ((string, string, string)?)p).LastOrDefault();
        }
    }
}
