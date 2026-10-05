using System.Text.Json;
using System.Text.Json.Nodes;
using app.Attributes;
using Display = global::app.module.screen.type.screen.display.@this;
using FilePath = global::app.type.item.path.file.@this;
using Program = global::app.module.terminal.type.process.@this;

namespace app.module.browser.type.browser.screen;

/// <summary>
/// A browser drawing onto PlangOS's display: Chromium runs as a normal browser there, each page in a window of its
/// own — the first the desktop (<c>%browser.desktop%</c>, the whole screen), more with <c>window.open</c>. The screen
/// makes the frames and gives it a real pointer and keyboard. A page of plang's own (under the app's or the os folder)
/// may talk with plang: <c>plang(text)</c> reaches OnMessage. A page opened as a tab becomes a window of its own.
/// Chromium is started by <c>/system/browser/Screen</c>, blank; each page it shows is given over DevTools.
/// </summary>
public sealed class @this : browser.@this
{
    private readonly Display _display;
    // where plang's own pages are: the app's folder and the runtime's os folder (the system's pages — only the
    // system writes there). They may talk with plang; no other page.
    private readonly FilePath _app, _os;
    private global::app.module.window.type.window.list.@this? _window;
    // the desktop's browser window: a page that lands in it was opened as a tab
    private int _desktopWindow;

    private @this(string url, Display display, Program program, cdp.@this cdp, FilePath app, FilePath os,
        Func<global::app.error.Error, Task> report)
        : base(url, display.Width.ToInt32(), display.Height.ToInt32(), program, cdp)
    {
        _display = display;
        _app = app;
        _os = os;
        Report = report;
    }

    private protected override string Lost => ": the screen's windows are gone until PlangOS starts again";

    /// <summary>The desktop: the first page, the whole screen.</summary>
    [LlmBuilder, Out] public global::app.module.window.type.window.@this Desktop => window.Desktop;

    /// <summary>Its windows on the screen, by their number (<c>%browser.window[1].url%</c>; 0 is the desktop).</summary>
    public global::app.module.window.type.window.list.@this window => _window ??= new(this);

    /// <summary>The display it draws onto.</summary>
    internal Display Display => _display;

    /// <summary>Hands what a page of the app's own says (<c>plang(text)</c>) to OnMessage.</summary>
    internal Func<string, Task>? Message { get; private set; }

    // ---- a page's video the host plays itself (pass-through) ----------------------------------------

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(long window, int stream), video.@this> _videos = new();
    private int _nextVideo;
    private int _mediaSaid;      // media log lines sent up this second
    private long _mediaSince;

    /// <summary>The script every page runs before its own once the host has said what it decodes: the codecs, then
    /// os/system/browser/video.js. None until then — a page's video is Chromium's and goes as pixels.</summary>
    internal string? VideoScript { get; private set; }

    /// <summary>What window <paramref name="window"/>'s page says about its videos (<c>plangVideo(json)</c>): a stream
    /// starts, a chunk, its clock, it ends — each to its stream, which tells the host.</summary>
    internal Func<string, Task> Video(long window) => said =>
    {
        try
        {
            using var json = JsonDocument.Parse(said);
            var e = json.RootElement;
            var stream = e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : 0;
            switch (e.TryGetProperty("video", out var what) ? what.GetString() : null)
            {
                case "start":
                    var started = _videos[(window, stream)] = new video.@this((uint)Interlocked.Increment(ref _nextVideo),
                        (kind, message) => _display.Wayland.Frame.Media(kind, message));
                    _display.Wayland.Debug($"browser: window {window}'s page starts video {stream} ({(e.TryGetProperty("type", out var t) ? t.GetString() : "")}): stream {started.Id}");
                    break;
                case "chunk" when _videos.TryGetValue((window, stream), out var v):
                    var chunk = Convert.FromBase64String(e.GetProperty("data").GetString() ?? "");
                    v.Chunk(chunk, e.TryGetProperty("offset", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetDouble() : 0);
                    _display.Wayland.Debug($"browser: video {v.Id} (window {window}'s {stream}) took {chunk.Length} B; {v.Holding}");
                    break;
                case "clock" when e.TryGetProperty("ids", out var ids):
                    var page = _display.Wayland.PagePlace((int)window);
                    foreach (var each in ids.EnumerateArray())
                        if (_videos.TryGetValue((window, each.GetInt32()), out var playing)) playing.Clock(e, page);
                    break;
                case "abort" when _videos.TryGetValue((window, stream), out var aborted):
                    aborted.Abort();
                    break;
                case "end" when _videos.TryRemove((window, stream), out var ended):
                    ended.End();
                    break;
                case "media":
                    // a media player's own words (Chromium's media log), up to the host beside the frames — it keeps
                    // them (media.jsonl): what a video did when it stopped. At most twenty a second per window.
                    _display.Wayland.Debug($"browser: window {window}'s media player: {e.GetProperty("event").GetString()}");
                    if (Interlocked.Increment(ref _mediaSaid) <= 20 || Environment.TickCount64 - _mediaSince > 1000)
                    {
                        if (Environment.TickCount64 - _mediaSince > 1000) { _mediaSince = Environment.TickCount64; _mediaSaid = 1; }
                        _display.Wayland.Up(new JsonObject
                        {
                            ["media"] = new JsonObject
                            {
                                ["window"] = window, ["event"] = e.GetProperty("event").GetString(),
                                ["data"] = JsonNode.Parse(e.GetProperty("data").GetRawText()),
                            },
                        }.ToJsonString());
                    }
                    break;
                case "page":
                    // a new page in the window: the last one's videos end (a page that goes away says nothing)
                    foreach (var key in _videos.Keys.Where(k => k.window == window).ToList())
                        if (_videos.TryRemove(key, out var left)) left.End();
                    break;
            }
        }
        catch (JsonException)
        {
            // a page's line that isn't one of the stand-in's: not ours to read
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            // said, never swallowed: a stream that stops reading would only show as a video that stops
            if (Report != null)
                return Report(new global::app.error.ServiceError($"A page's video couldn't be read: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", "VideoUnread", 500) { Exception = ex });
        }
        return Task.CompletedTask;
    };

    // the host said what it decodes: every page gets the hook from its next load on
    private async Task Codecs(string[] codecs, global::app.actor.context.@this context)
    {
        if (codecs.Length == 0) return;
        var hook = await new FilePath(_os.Absolute + "/system/browser/video.js").Read(context);
        if (!hook.Success || (await hook.Value())?.ToString() is not { Length: > 0 } source)
        {
            // said, never quietly: the videos stay Chromium's (pixels) and nothing else would tell why
            if (Report != null)
                await Report(new global::app.error.ServiceError($"Video pass-through is off: the page hook didn't read ({hook.Error?.Message ?? "empty"})", "VideoHookUnread", 500));
            return;
        }
        VideoScript = "window.__plangCodecs=" + JsonSerializer.Serialize(codecs) + ";\n" + source;
        await window.Video(VideoScript);
        _display.Wayland.Debug($"browser: pages run the video hook from their next load (codecs {string.Join(", ", codecs)})");
    }

    /// <summary>The page at <paramref name="address"/> is one of plang's own: a file under the app's folder or the
    /// os folder.</summary>
    internal bool Own(string address)
        => Uri.TryCreate(address, UriKind.Absolute, out var url) && url.IsFile
           && new FilePath(url.LocalPath) is var page && (page.IsUnder(_app).Value || page.IsUnder(_os).Value);

    /// <summary>The address as a person reads it: one of plang's own pages as its plang path (<c>/Desktop/notes.txt</c>
    /// in the app, <c>/system/…</c> for the system's) — never <c>file://</c>; a website as it is.</summary>
    internal static string Shown(string address, global::app.actor.context.@this context)
        => Uri.TryCreate(address, UriKind.Absolute, out var url) && url.IsFile ? FilePath.Resolve(address, context).Raw : address;

    /// <summary>A page's address for its window: what the globe shows (<see cref="Shown"/>), and the page itself.</summary>
    internal static global::app.module.screen.type.screen.display.code.Address AddressOf(string address, global::app.actor.context.@this context)
        => new(Shown(address, context), address);

    /// <summary>Starts Chromium on <paramref name="display"/>, its first page <paramref name="url"/> the desktop; what a
    /// page of plang's own says goes to <paramref name="onMessage"/>.</summary>
    internal static async Task<global::app.data.@this<browser.@this>> Start(Display display, string url,
        global::app.goal.step.action.@this? onMessage, global::app.actor.context.@this context)
    {
        if (await Readable(url, context) is { } refused) return global::app.data.@this<browser.@this>.From(refused);
        var (program, failed) = await Started("Screen", context);
        if (program == null) return global::app.data.@this<browser.@this>.From(failed!);
        var cdp = new cdp.@this(program.pipe!);
        var browser = new @this(url, display, program, cdp,
            new FilePath(context.App.AbsolutePath), new FilePath(context.App.OsAbsolutePath),
            error => context.Actor.Channel.Report(context.Error(error)));
        // Chromium stopping by itself is said, with what it said last — never a black screen without a word
        browser.Watched();
        if (onMessage != null)
            browser.Message = said => global::app.module.on.code.Gate.Call(onMessage, Payload(said, context), context);
        // OnMessage asks to hear the page, but only plang's own pages may talk: say so, rather than a page
        // that waits for plang() forever
        if (onMessage != null && !browser.Own(url))
            await context.Actor.Channel.Report(context.Error(new global::app.error.ActionError(
                $"browser.start: {url} is not one of plang's own pages (under the app's or the os folder), so it gets no plang() and OnMessage never hears it",
                "PageNotOwn", 400)));
        try
        {
            // the desktop is the first page, its app window blank: shown as window 0, then sent where it goes
            var desktop = await FirstPage(cdp);
            await browser.window.ShowDesktop(desktop, url);
            if (await browser.Desktop.Navigate(url, context) is { Success: false } refusedThere) return global::app.data.@this<browser.@this>.From(refusedThere);
            display.Followed += browser.window.Follow;
            // the host's codecs, now (known already) or when it says them: pages' videos go to it from then on
            display.Wayland.CodecsKnown += codecs => _ = browser.Codecs(codecs, context);
            if (display.Wayland.Codecs.Length > 0) await browser.Codecs(display.Wayland.Codecs, context);
            await browser.Watch(desktop);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException or KeyNotFoundException)
        {
            program.Kill();
            return Fail(context, $"Chromium didn't open its desktop: {ex.Message}", "BrowserStartFailed", 500);
        }
        return context.Ok<browser.@this>(browser);
    }

    // the page Chromium opened (its blank app window), once DevTools lists it
    private static async Task<string> FirstPage(cdp.@this cdp)
    {
        for (var tries = 0; tries < 120; tries++)
        {
            if ((await cdp.Pages()).FirstOrDefault() is { target: { } target }) return target;
            await Task.Delay(250);
        }
        throw new TimeoutException("Chromium has no page to show");
    }

    /// <summary>A page in a window of its own (<c>window.open</c>): an app window on plang's blank page handed to the running Chromium
    /// (<c>/system/browser/Window</c>), then sent to <paramref name="url"/> — read as the caller when it is a file.</summary>
    internal async Task<global::app.data.@this> Open(string url, global::app.actor.context.@this context)
    {
        if (await Readable(url, context) is { } refused) return refused;
        var before = (await Cdp.Pages()).Select(p => p.target).ToHashSet();
        var found = await context.App.goal.list.Find("/system/browser/Window", context.call.Goal);
        if (!found.Success || await found.Value() is not { } goal) return found;
        var ran = await goal.Start(context, []);
        if (!ran.Success) return ran;
        // the new blank window: a page DevTools lists now that it didn't before
        for (var tries = 0; tries < 40; tries++)
        {
            if ((await Cdp.Pages()).FirstOrDefault(p => !before.Contains(p.target)) is { target: { } fresh })
            {
                var page = await Cdp.Attach(fresh);
                try { await page.Ask("Page.navigate", new JsonObject { ["url"] = url }); }
                finally { await page.Detach(); }
                return context.Ok();
            }
            await Task.Delay(250);
        }
        // what the hand-over said is part of why: its exit code and its stderr
        var said = (await ran.Properties.Get<object>("Error"))?.ToString();
        var code = (await ran.Properties.Get<object>("ExitCode"))?.ToString();
        var pages = string.Join(", ", (await Cdp.Pages()).Select(p => p.url));
        return context.Error(new global::app.error.ActionError(
            $"Chromium opened no window for {url} (the hand-over exited {code ?? "?"}{(string.IsNullOrWhiteSpace(said) ? "" : ", saying: " + said)}; its pages: {pages})",
            "WindowNotOpened", 500));
    }

    /// <summary>On a screen, input is the screen's: the value goes to the display whole.</summary>
    internal override Task<global::app.data.@this> Send(global::app.type.item.input.@this input, global::app.actor.context.@this context)
    {
        _display.Wayland.Take(input);
        return Task.FromResult(context.Ok());
    }

    /// <summary>A line from the page as <c>%!data%</c>: a dict when it is a json object, so a goal can read its
    /// fields; the text otherwise.</summary>
    private static global::app.data.@this Payload(string line, global::app.actor.context.@this context)
    {
        try
        {
            using var json = JsonDocument.Parse(line);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
                return new global::app.data.@this("!data", new global::app.type.item.serializer.json(context).Parse(json.RootElement.Clone()), context: context);
        }
        catch (JsonException) { }
        return new global::app.data.@this("!data", line, context.App.type.list["text"], context: context);
    }

    // ---- new tabs become windows ------------------------------------------------------------------

    /// <summary>Watches the browser's pages. A page opened as a tab (Ctrl+click, a link to a new tab) lands in the only
    /// tabbed window, the desktop, and would cover the screen: it is closed there and told to the screen as
    /// <c>{"open": url}</c> — its OnWindow opens it as a window of its own.</summary>
    private async Task Watch(string desktop)
    {
        var window = await Cdp.Ask("Browser.getWindowForTarget", new JsonObject { ["targetId"] = desktop });
        _desktopWindow = window.GetProperty("result").GetProperty("windowId").GetInt32();
        Cdp.Heard += (method, parameters, session) =>
        {
            if (session != null || method is not ("Target.targetCreated" or "Target.targetInfoChanged")) return Task.CompletedTask;
            var info = parameters.GetProperty("targetInfo");
            var target = info.GetProperty("targetId").GetString()!;
            var url = info.GetProperty("url").GetString() ?? "";
            if (info.GetProperty("type").GetString() != "page" || target == this.window.Desktop.Target
                || url.Length == 0 || url == "about:blank") return Task.CompletedTask;
            return Tab(target, url);
        };
        await Cdp.Ask("Target.setDiscoverTargets", new JsonObject { ["discover"] = true });
    }

    private async Task Tab(string target, string url)
    {
        if (!window.First(target)) return;   // each page is looked at once
        try
        {
            var reply = await Cdp.Ask("Browser.getWindowForTarget", new JsonObject { ["targetId"] = target });
            if (reply.GetProperty("result").GetProperty("windowId").GetInt32() != _desktopWindow) return;
        }
        catch (Exception ex) when (ex is TimeoutException or KeyNotFoundException or InvalidOperationException or IOException) { return; }
        await Cdp.Tell("Target.closeTarget", new JsonObject { ["targetId"] = target });
        _display.Wayland.Tell(new JsonObject { ["open"] = url });
    }
}
