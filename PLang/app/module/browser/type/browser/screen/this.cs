using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using app.Attributes;
using Display = global::app.module.screen.type.screen.display.@this;
using FilePath = global::app.type.item.path.file.@this;
using Text = global::app.type.item.text.@this;

namespace app.module.browser.type.browser.screen;

/// <summary>
/// A browser drawing onto PlangOS's display: Chromium runs as a normal browser there, each page in a window of its
/// own — the first the desktop (<c>%browser.desktop%</c>, the whole screen), more with <c>window.open</c>. The screen
/// makes the frames and gives it a real pointer and keyboard. A page of plang's own (under the app's or the os folder)
/// may talk with plang: <c>plang(text)</c> reaches OnMessage. A page opened as a tab becomes a window of its own.
/// </summary>
public sealed class @this : browser.@this
{
    private readonly Display _display;
    private readonly FilePath _chromium, _profile;
    private global::app.module.window.type.window.list.@this? _window;
    // DevTools for the whole browser (its pages and windows); null when it couldn't be reached
    private ClientWebSocket? _control;
    // the desktop's browser window: a page that lands in it was opened as a tab
    private int _desktopWindow;

    private @this(string url, Display display, Process os, FilePath chromium, FilePath profile, int port, string[] roots,
        Func<global::app.error.Error, Task> report)
        : base(url, display.PixelWidth, display.PixelHeight, os)
    {
        _display = display;
        _chromium = chromium;
        _profile = profile;
        Port = port;
        Roots = roots;
        Report = report;
    }

    private protected override string Variant => "screen";

    private protected override ClientWebSocket? Speaks => _control;

    private protected override string Lost => ": the screen's windows are gone until PlangOS starts again";

    /// <summary>The desktop: the first page, the whole screen.</summary>
    [LlmBuilder, Out] public global::app.module.window.type.window.@this Desktop => window.Desktop;

    /// <summary>Its windows on the screen, by their number (<c>%browser.window[1].url%</c>; 0 is the desktop).</summary>
    public global::app.module.window.type.window.list.@this window => _window ??= new(this);

    /// <summary>One step by dot: <c>window</c> — its windows; any other member as every item's.</summary>
    public override ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "window", StringComparison.OrdinalIgnoreCase)
            ? ValueTask.FromResult(new global::app.data.@this(key, window, parent: parent))
            : base.Get(parent, key);

    /// <summary>DevTools' port, on 127.0.0.1: the pages of its windows.</summary>
    internal int Port { get; }

    /// <summary>The display it draws onto.</summary>
    internal Display Display => _display;

    /// <summary>Hands what a page of the app's own says (<c>plang(text)</c>) to OnMessage.</summary>
    internal Func<string, Task>? Message { get; private set; }

    /// <summary>Where plang's own pages are (<c>file://</c> under these folders): the app's folder, and the
    /// runtime's os folder (the system's pages — only the system writes there). They may talk with plang;
    /// no other page.</summary>
    private string[] Roots { get; }

    /// <summary>The page at <paramref name="address"/> is one of plang's own.</summary>
    internal bool Own(string address) => Roots.Any(root => address.StartsWith(root, StringComparison.Ordinal));

    /// <summary>The address as a person reads it: one of plang's own pages as its plang path — in the app
    /// from its root (<c>/Desktop/notes.txt</c>), in the os folder under <c>/os/</c> — never
    /// <c>file://</c>; a website as it is.</summary>
    internal string Shown(string address)
    {
        if (Roots.Length > 0 && address.StartsWith(Roots[0], StringComparison.Ordinal))
            return "/" + Uri.UnescapeDataString(address[Roots[0].Length..]);
        if (Roots.Length > 1 && address.StartsWith(Roots[1], StringComparison.Ordinal))
            return "/os/" + Uri.UnescapeDataString(address[Roots[1].Length..]);
        return address;
    }

    /// <summary>A page's address for its window: what the globe shows (<see cref="Shown"/>), and the page itself.</summary>
    internal global::app.module.screen.type.screen.display.code.Address AddressOf(string address) => new(Shown(address), address);

    /// <summary>Starts <paramref name="chromium"/> on <paramref name="display"/>, its first page <paramref name="url"/>
    /// the desktop; what a page of plang's own says goes to <paramref name="onMessage"/>.</summary>
    internal static async Task<global::app.data.@this<browser.@this>> Start(FilePath chromium, Display display, string url,
        global::app.goal.step.action.@this? onMessage, global::app.actor.context.@this context)
    {
        // The first page is the desktop: an app window (no tabs or toolbar), which the screen makes the
        // whole screen — not fullscreen, where Chromium shows its "press Esc to exit" bubble. DevTools,
        // on 127.0.0.1 inside PlangOS, lets the page talk to plang: plang(text) → OnMessage,
        // window.post → the page's message event.
        var (profile, refused) = await Profile(context);
        if (profile == null) return global::app.data.@this<browser.@this>.From(refused!);
        // a Chromium that was killed (PlangOS closed hard) leaves its profile's lock naming its process;
        // in a new PlangOS another process can have that number, and the new Chromium would hand its
        // page to "the running one" and exit. None of ours runs yet: the lock is stale. (Not there: NotFound, and fine.)
        foreach (var name in new[] { "SingletonLock", "SingletonSocket", "SingletonCookie" })
            await FilePath.Resolve(Profiled + "/" + name, context).Delete(recursive: false, context);
        var chrome = Process.Start(Chrome(chromium, display, profile, context, "--remote-debugging-address=127.0.0.1", "--remote-debugging-port=0", "--app=" + url))!;
        chrome.StandardInput.Close();
        _ = Drain(chrome.StandardOutput);
        // what Chromium says on its error output is kept (its last words, if it stops) and goes to --debug
        @this? started = null;
        var early = new List<string>();
        var port = await PortOf(chrome.StandardError, TimeSpan.FromSeconds(30), line =>
        {
            if (started != null) started.Heard(line);
            else lock (early) early.Add(line);
            return context.App.Debug?.Write("chromium: " + line) ?? Task.CompletedTask;
        });
        var pageUrl = port == null ? null : await PageOf(port.Value);
        if (pageUrl == null)
        {
            chrome.Kill(entireProcessTree: true);
            return Fail(context, "Chromium didn't open DevTools on its first page within 30 seconds.", "BrowserStartFailed", 500);
        }

        var roots = new[] { context.App.AbsolutePath, context.App.OsAbsolutePath }
            .Where(folder => !string.IsNullOrEmpty(folder)).Select(folder => new Uri(folder!.TrimEnd('/') + "/").AbsoluteUri).ToArray();
        var browser = new @this(url, display, chrome, chromium, profile, port!.Value, roots,
            error => context.Actor.Channel.Report(context.Error(error)));
        lock (early) foreach (var line in early) browser.Heard(line);
        started = browser;
        // Chromium stopping by itself is said, with what it said last — never a black screen without a word
        browser.Watched();
        if (onMessage != null)
            browser.Message = said => global::app.module.on.code.Gate.Call(onMessage, Payload(said, context), context);
        // OnMessage asks to hear the page, but only plang's own pages may talk: say so, rather than a page
        // that waits for plang() forever
        if (onMessage != null && !browser.Own(url))
            await context.Actor.Channel.Report(context.Error(new global::app.error.ActionError(
                $"browser.start: {url} is not one of plang's own pages (under {string.Join(" or ", roots)}), so it gets no plang() and OnMessage never hears it",
                "PageNotOwn", 400)));
        // the desktop is window 0; each other window is paired with its page as it gets a title
        var desktop = pageUrl[(pageUrl.LastIndexOf('/') + 1)..];
        await browser.window.ShowDesktop(desktop, url);
        display.Wayland.Told += browser.window.Follow;
        await browser.Controlled(desktop, context);
        return context.Ok<browser.@this>(browser);
    }

    // the whole browser, for pages opened as tabs: they become windows (Watch)
    private async Task Controlled(string desktop, global::app.actor.context.@this context)
    {
        try
        {
            using var version = JsonDocument.Parse(await DevTools.GetStringAsync($"http://127.0.0.1:{Port}/json/version"));
            _control = new ClientWebSocket();
            await _control.ConnectAsync(new Uri(version.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!), CancellationToken.None);
            _ = Task.Run(Watch);
            using var desktopWindow = JsonDocument.Parse(await Ask("Browser.getWindowForTarget", new JsonObject { ["targetId"] = desktop }));
            _desktopWindow = desktopWindow.RootElement.GetProperty("result").GetProperty("windowId").GetInt32();
            await Cdp("Target.setDiscoverTargets", new JsonObject { ["discover"] = true });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException or KeyNotFoundException or WebSocketException)
        {
            await (context.App.Debug?.Write("browser: no DevTools browser connection; new tabs stay tabs: " + ex.Message) ?? Task.CompletedTask);
        }
    }

    /// <summary>Chromium drawing onto the display, with the app's own profile: the first start runs
    /// it, a later one hands its page to the running one and exits.</summary>
    private static ProcessStartInfo Chrome(FilePath chromium, Display display, FilePath profile, global::app.actor.context.@this context,
        params string[] args)
    {
        var info = new ProcessStartInfo(chromium.Absolute)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = context.App.AbsolutePath,
        };
        foreach (var arg in new[] {
            "--ozone-platform=wayland", "--no-first-run", "--no-default-browser-check",
            // no GPU in PlangOS: pages are composited in software, and WebGL runs on the CPU (SwiftShader).
            // --disable-gpu turned WebGL off entirely, which bot checks (Cloudflare) look for; no
            // measurable memory cost (2026-09-29, mbl.is: 589 MB/11 processes without, 532 MB/9 with)
            "--disable-gpu-compositing", "--use-angle=swiftshader", "--enable-unsafe-swiftshader",
            "--num-raster-threads=4",   // CPU rendering: rasterize on more cores
            "--hide-crash-restore-bubble",   // PlangOS ends Chromium when the host closes: not a crash to report
            // no HTTP/3: over QUIC (UDP) through WSL's network YouTube's video fetches starve (measured
            // 2026-09-30: 0.9 MB in 40 s after a seek, then "something went wrong"; over TCP 17 MB)
            "--disable-quic",
            // with its DevTools port open (how plang drives it) Chromium says it is automated
            // (navigator.webdriver): sites treat it as a bot — RÚV shows "ok" instead of its player,
            // Cloudflare won't let it through. PlangOS's browser is the user's browser: it doesn't say so.
            "--disable-blink-features=AutomationControlled",
            // memory (measured, 2026-09-29: Chromium 430 → 351 MB PSS, 12 → 9 processes):
            "--in-process-gpu",   // no GPU process: with the GPU off it only composites, in the browser process as well
            // no spare renderer kept waiting for a next site; no preloaded address-bar drop-downs
            // (PlangOS draws its own address field)
            "--disable-features=SpareRendererForSitePerProcess,WebUIOmniboxPopup,WebUIOmniboxAimPopup",
            $"--user-data-dir={profile.Absolute}" }.Concat(args))
            info.ArgumentList.Add(arg);
        // sound: PulseAudio finds its server in XDG_RUNTIME_DIR unless PULSE_SERVER names one — and
        // XDG_RUNTIME_DIR is the screen's now, where there is none. WSL's own (WSLg's server) is named.
        var sound = Environment.GetEnvironmentVariable("PULSE_SERVER");
        if (string.IsNullOrEmpty(sound))
        {
            var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            sound = string.IsNullOrEmpty(runtime) ? "unix:/mnt/wslg/PulseServer" : $"unix:{runtime}/pulse/native";
        }
        info.Environment["PULSE_SERVER"] = sound;
        info.Environment["XDG_RUNTIME_DIR"] = display.Runtime;   // where the screen's socket is
        info.Environment["WAYLAND_DISPLAY"] = display.Socket;
        return info;
    }

    /// <summary>A page in a window of its own (<c>window.open</c>): an app window, title bar only.
    /// The running Chromium opens it; the one started here hands it over and exits.</summary>
    internal void Open(string url, global::app.actor.context.@this context)
    {
        var handOver = Process.Start(Chrome(_chromium, _display, _profile, context, "--app=" + url))!;
        handOver.StandardInput.Close();
        _ = Drain(handOver.StandardOutput);
        _ = Drain(handOver.StandardError);
    }

    /// <summary>On a screen, input is the screen's: passed on, the Data written as text.</summary>
    internal override async Task<global::app.data.@this> Send(global::app.data.@this data, global::app.actor.context.@this context)
    {
        using var written = new MemoryStream();
        await Text.Encode(written, data, context, null, null, CancellationToken.None);
        _display.Wayland.Input(Encoding.UTF8.GetString(written.ToArray()));
        return context.Ok();
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

    /// <summary>Watches the browser's pages. A page opened as a tab (Ctrl+click, a link to a new tab)
    /// lands in the only tabbed window, the desktop, and would cover the screen: it is closed there
    /// and told to the screen as <c>{"open": url}</c> — its OnWindow opens it as a window of its own.</summary>
    private async Task Watch()
    {
        var buffer = new byte[1 << 16];
        var message = new MemoryStream();
        try
        {
            while (_control!.State == WebSocketState.Open)
            {
                var r = await _control.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (!r.EndOfMessage) continue;
                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id))
                {
                    Answered(id, text);
                    continue;
                }
                var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                if (method is not ("Target.targetCreated" or "Target.targetInfoChanged")) continue;
                var info = root.GetProperty("params").GetProperty("targetInfo");
                var target = info.GetProperty("targetId").GetString()!;
                var url = info.GetProperty("url").GetString() ?? "";
                if (info.GetProperty("type").GetString() != "page" || target == window.Desktop.Target
                    || url.Length == 0 || url == "about:blank") continue;
                // not awaited: it asks the browser, whose answer this loop reads
                _ = Task.Run(() => Tab(target, url));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidOperationException) { }
    }

    private async Task Tab(string target, string url)
    {
        if (!window.First(target)) return;   // each page is looked at once
        try
        {
            using var reply = JsonDocument.Parse(await Ask("Browser.getWindowForTarget", new JsonObject { ["targetId"] = target }));
            if (reply.RootElement.GetProperty("result").GetProperty("windowId").GetInt32() != _desktopWindow) return;
        }
        catch (Exception ex) when (ex is TimeoutException or JsonException or KeyNotFoundException or InvalidOperationException) { return; }
        await Cdp("Target.closeTarget", new JsonObject { ["targetId"] = target });
        _display.Wayland.Tell(new JsonObject { ["open"] = url });
    }
}
