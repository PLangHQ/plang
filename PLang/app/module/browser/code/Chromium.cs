using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using app.error;
using app.Utils;
using Text = app.type.item.text.@this;
using FilePath = app.type.item.path.file.@this;

namespace app.module.browser.code;

/// <summary>
/// Headless Chromium over DevTools (the Chrome DevTools Protocol, on a WebSocket bound to
/// 127.0.0.1). Frames come from <c>Page.startScreencast</c>, each acked at once so Chromium keeps
/// sending; input goes in through <c>Input.dispatch*</c>. Chromium is the system's own browser, started
/// by this module (capability "browser"), not a program the goal names — so no execute prompt: the
/// app's input may be a pipe (PlangOS), where a prompt would take a line meant for the browser.
/// </summary>
public sealed partial class Chromium : IBrowser
{
    public string Name { get; init; } = "chromium";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // Headless Chromium says "HeadlessChrome" in its user agent; bot checks (Cloudflare) stop there.
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0.0.0 Safari/537.36";
    private static readonly string[] Programs = ["chromium", "chromium-browser", "google-chrome", "//usr/lib/chromium/chromium"];

    [GeneratedRegex(@"DevTools listening on ws://127\.0\.0\.1:(\d+)/")]
    private static partial Regex Listening();

    public async Task<data.@this<Browser>> Start(start action)
    {
        var context = action.Context;
        var program = Programs.Select(p => FilePath.Program(p, context)).FirstOrDefault(p => p != null);
        if (program == null)
            return Fail(context, "No Chromium found (chromium on PATH, or /usr/lib/chromium/chromium).", "BrowserNotFound", 404);

        // On a screen (PlangOS's display), Chromium runs as a normal browser drawing onto it: the
        // screen sends only what changed, and gives it a real pointer and keyboard.
        if (action.Screen != null && await action.Screen.Value() is { Display: not null } screen)
            return await OnScreen(action, program, screen);

        // headless over DevTools: what the screencast needs, read here where it is used
        var url = (await action.Url.Value())!.Clr<string>()!;
        var width = (int)(await action.Width.Value())!.ToDouble();
        var height = (int)(await action.Height.Value())!.ToDouble();
        var format = (await action.Format.Value())!.Clr<string>()!;
        var quality = (int)(await action.Quality.Value())!.ToDouble();
        var onFrame = action.OnFrame == null ? null : await action.OnFrame.Value();

        var info = new ProcessStartInfo(program.Absolute)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,     // never the app's own input: in PlangOS that is the pipe from the host
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = context.App.AbsolutePath,
        };
        foreach (var arg in new[] {
            "--headless", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
            $"--window-size={width},{height}",
            $"--user-data-dir={PathHelper.Combine(context.App.AbsolutePath, ".browser")}",
            "--remote-debugging-address=127.0.0.1", "--remote-debugging-port=0",   // Chromium picks a free port
            $"--user-agent={UserAgent}", url })
            info.ArgumentList.Add(arg);

        var os = Process.Start(info)!;
        os.StandardInput.Close();
        _ = Drain(os.StandardOutput);
        // Chromium says which port it took on stderr; keep reading stderr after that, or it blocks.
        var port = await PortOf(os.StandardError, TimeSpan.FromSeconds(30));
        if (port == null)
        {
            os.Kill(entireProcessTree: true);
            return Fail(context, "Chromium didn't open DevTools within 30 seconds.", "BrowserStartFailed", 500);
        }

        var pageUrl = await PageOf(port.Value);
        if (pageUrl == null)
        {
            os.Kill(entireProcessTree: true);
            return Fail(context, "Chromium has no page to show.", "BrowserStartFailed", 500);
        }

        var browser = new Browser { Url = url, Width = width, Height = height, Os = os, Page = new ClientWebSocket() };
        browser.Page.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        await browser.Page.ConnectAsync(new Uri(pageUrl), CancellationToken.None);
        await Cdp(browser, "Page.enable", new JsonObject());
        await Cdp(browser, "Page.startScreencast", new JsonObject
        {
            ["format"] = format, ["quality"] = quality, ["maxWidth"] = width, ["maxHeight"] = height, ["everyNthFrame"] = 1,
        });

        // Newest frame only: a slow OnFrame skips frames rather than falling behind.
        var frames = System.Threading.Channels.Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        _ = Task.Run(() => Receive(browser, format, frames.Writer));
        if (onFrame != null)
        {
            Task Deliver(string line) => global::app.module.on.code.Gate.Call(onFrame,
                new data.@this("!data", line, context.App.type.list["text"], context: context), context);
            browser.Emit = Deliver;
            _ = Task.Run(async () =>
            {
                await foreach (var line in frames.Reader.ReadAllAsync()) await Deliver(line);
            });
        }

        return context.Ok<Browser>(browser);
    }

    private static async Task<data.@this<Browser>> OnScreen(start action, FilePath chromium, global::app.module.screen.Screen screen)
    {
        var context = action.Context;
        var display = screen.Display!;

        // The first page is the desktop: an app window (no tabs or toolbar), which the screen makes the
        // whole screen — not fullscreen, where Chromium shows its "press Esc to exit" bubble. DevTools,
        // on 127.0.0.1 inside PlangOS, lets the page talk to plang: plang(text) → OnMessage,
        // browser.post → the page's message event.
        var url = (await action.Url.Value())!.Clr<string>()!;
        // a Chromium that was killed (PlangOS closed hard) leaves its profile's lock naming its process;
        // in a new PlangOS another process can have that number, and the new Chromium would hand its
        // page to "the running one" and exit. None of ours runs yet: the lock is stale.
        foreach (var name in new[] { "SingletonLock", "SingletonSocket", "SingletonCookie" })
            await FilePath.Resolve(PathHelper.Combine(context.App.AbsolutePath, ".browser", name), context).Delete(false, true, context);
        var chrome = Process.Start(Chrome(chromium, screen, context, "--remote-debugging-address=127.0.0.1", "--remote-debugging-port=0", "--app=" + url))!;
        chrome.StandardInput.Close();
        _ = Drain(chrome.StandardOutput);
        var port = await PortOf(chrome.StandardError, TimeSpan.FromSeconds(30), line => context.App.Debug?.Write("chromium: " + line) ?? Task.CompletedTask);
        var pageUrl = port == null ? null : await PageOf(port.Value);
        if (pageUrl == null)
        {
            chrome.Kill(entireProcessTree: true);
            return Fail(context, "Chromium didn't open DevTools on its first page within 30 seconds.", "BrowserStartFailed", 500);
        }

        var browser = new Browser
        {
            Url = url, Width = screen.Width, Height = screen.Height, Os = chrome, Screen = screen, Program = chromium, Port = port!.Value,
            Root = new Uri(context.App.AbsolutePath.TrimEnd('/') + "/").AbsoluteUri,
        };
        var onMessage = action.OnMessage == null ? null : await action.OnMessage.Value();
        if (onMessage != null)
            browser.Message = said => global::app.module.on.code.Gate.Call(onMessage, Payload(said, context), context);
        // the desktop is window 0; each other window is paired with its page as it gets a title
        var desktop = pageUrl[(pageUrl.LastIndexOf('/') + 1)..];
        await browser.Windows.ShowDesktop(desktop, url);
        display.Told += browser.Windows.Follow;

        // the whole browser, for pages opened as tabs: they become windows (Watch)
        try
        {
            using var version = JsonDocument.Parse(await DevTools.GetStringAsync($"http://127.0.0.1:{browser.Port}/json/version"));
            browser.Control = new ClientWebSocket();
            await browser.Control.ConnectAsync(new Uri(version.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!), CancellationToken.None);
            _ = Task.Run(() => Watch(browser));
            using var desktopWindow = JsonDocument.Parse(await Ask(browser, "Browser.getWindowForTarget", new JsonObject { ["targetId"] = desktop }, browser.Control));
            browser.DesktopWindow = desktopWindow.RootElement.GetProperty("result").GetProperty("windowId").GetInt32();
            await Cdp(browser, "Target.setDiscoverTargets", new JsonObject { ["discover"] = true }, browser.Control);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException or KeyNotFoundException or WebSocketException)
        {
            await (context.App.Debug?.Write("browser: no DevTools browser connection; new tabs stay tabs: " + ex.Message) ?? Task.CompletedTask);
        }
        return context.Ok<Browser>(browser);
    }

    /// <summary>Chromium drawing onto the screen, with the app's own profile: the first start runs
    /// it, a later one hands its page to the running one and exits.</summary>
    private static ProcessStartInfo Chrome(FilePath chromium, global::app.module.screen.Screen screen, actor.context.@this context, params string[] args)
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
            // memory (measured, 2026-09-29: Chromium 430 → 351 MB PSS, 12 → 9 processes):
            "--in-process-gpu",   // no GPU process: with the GPU off it only composites, in the browser process as well
            // no spare renderer kept waiting for a next site; no preloaded address-bar drop-downs
            // (PlangOS draws its own address field)
            "--disable-features=SpareRendererForSitePerProcess,WebUIOmniboxPopup,WebUIOmniboxAimPopup",
            $"--user-data-dir={PathHelper.Combine(context.App.AbsolutePath, ".browser")}" }.Concat(args))
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
        info.Environment["XDG_RUNTIME_DIR"] = screen.Runtime;   // where the screen's socket is
        info.Environment["WAYLAND_DISPLAY"] = screen.Socket;
        return info;
    }

    /// <summary>A line from the screen or the page as <c>%!data%</c>: a dict when it is a json object,
    /// so a goal can read its fields; the text otherwise.</summary>
    private static data.@this Payload(string line, actor.context.@this context)
    {
        try
        {
            using var json = JsonDocument.Parse(line);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
                return new data.@this("!data", new global::app.type.item.serializer.json(context).Parse(json.RootElement.Clone()), context: context);
        }
        catch (JsonException) { }
        return new data.@this("!data", line, context.App.type.list["text"], context: context);
    }

    private static readonly HttpClient DevTools = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>A page in a window of its own (<c>window.open</c>): an app window, title bar only.
    /// The running Chromium opens it; the one started here hands it over and exits.</summary>
    internal static void Open(Browser browser, string url, actor.context.@this context)
    {
        if (browser is not { Program: { } program, Screen: { } screen }) return;
        var handOver = Process.Start(Chrome(program, screen, context, "--app=" + url))!;
        handOver.StandardInput.Close();
        _ = Drain(handOver.StandardOutput);
        _ = Drain(handOver.StandardError);
    }


    public async Task<data.@this> Send(send action)
    {
        var context = action.Context;
        var browser = await action.Browser.Value();
        if (browser?.Screen?.Display is { } display)
        {
            // on a screen, input is the screen's (screen.send): passed on, the Data written as text
            using var written = new MemoryStream();
            await Text.Encode(written, action.Data, context, null, null, CancellationToken.None);
            display.Input(Encoding.UTF8.GetString(written.ToArray()));
            return context.Ok();
        }
        if (browser?.Page is not { State: WebSocketState.Open })
            return context.Error(new ActionError($"The browser isn't running: {browser}", "BrowserNotRunning", 409));
        var value = await action.Data.Value();
        var line = value is Text t ? t.Clr<string>() : value?.ToString();
        if (string.IsNullOrWhiteSpace(line)) return context.Ok();

        JsonNode? input;
        try { input = JsonNode.Parse(line); }
        catch (JsonException) { return context.Ok(); }   // not an input event: ignored
        if (input is not JsonObject e) return context.Ok();

        var (method, parameters) = Translate(e);
        if (method == null) return context.Ok();
        await Cdp(browser, method, parameters!);
        if (method == "Input.dispatchMouseEvent" && parameters!["type"]?.GetValue<string>() == "mouseMoved")
            AskCursor(browser, parameters["x"]!.GetValue<int>(), parameters["y"]!.GetValue<int>());
        return context.Ok();
    }

    // The frames don't carry the pointer. After a move (at most 20 times a second, one question at a
    // time) ask the page what is under the mouse and send {"cursor":…} up when it changes. Not awaited:
    // this runs inside the input call, and the answer goes out through the same one-at-a-time gate.
    private static void AskCursor(Browser browser, int x, int y)
    {
        var now = Environment.TickCount64;
        if (browser.Emit == null || now - browser.LastCursorAsk < 50) return;
        if (Interlocked.CompareExchange(ref browser.CursorBusy, 1, 0) != 0) return;
        browser.LastCursorAsk = now;
        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await Ask(browser, "Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "(function(){var e=document.elementFromPoint(" + x + "," + y + ");if(!e)return 'default';" +
                        "var c=getComputedStyle(e).cursor;if(c!=='auto')return c;if(e.closest('a[href],button,[role=button],label'))return 'pointer';" +
                        "if(e.isContentEditable||/^(INPUT|TEXTAREA)$/.test(e.tagName))return 'text';return 'default'})()",
                    ["returnByValue"] = true,
                });
                using var doc = JsonDocument.Parse(reply);
                if (!doc.RootElement.TryGetProperty("result", out var r) || !r.TryGetProperty("result", out var v)
                    || !v.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return;
                var cursor = value.GetString()!;
                if (cursor == browser.LastCursor) return;
                browser.LastCursor = cursor;
                await browser.Emit!("{\"cursor\":" + JsonSerializer.Serialize(cursor) + "}");
            }
            catch { /* a lost answer only means the pointer stays as it was */ }
            finally { Interlocked.Exchange(ref browser.CursorBusy, 0); }
        });
    }

    public async Task<data.@this> Stop(stop action)
    {
        var browser = await action.Browser.Value();
        if (browser == null) return action.Context.Ok();
        // the whole browser's connection on a screen, the page's off-screen
        var speaks = browser.Control is { State: WebSocketState.Open } control ? control : browser.Page;
        try { if (speaks is { State: WebSocketState.Open }) await Cdp(browser, "Browser.close", new JsonObject(), speaks); } catch { }
        if (browser.Os is { HasExited: false } os && !os.WaitForExit(1000)) os.Kill(entireProcessTree: true);
        return action.Context.Ok();
    }

    // ---- input: the neutral event lines → DevTools ---------------------------------------------

    private static (string? method, JsonObject? parameters) Translate(JsonObject e)
    {
        int Int(string key) => e[key] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0;
        string Str(string key, string fallback = "") => e[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

        if (e["mouse"] != null)
        {
            var kind = Str("mouse");
            var p = new JsonObject { ["x"] = Int("x"), ["y"] = Int("y"), ["modifiers"] = Int("mods") };
            switch (kind)
            {
                case "wheel":
                    p["type"] = "mouseWheel"; p["deltaX"] = Int("dx"); p["deltaY"] = Int("dy");
                    break;
                case "down": case "up": case "move":
                    p["type"] = kind == "down" ? "mousePressed" : kind == "up" ? "mouseReleased" : "mouseMoved";
                    p["button"] = Str("button", "none");
                    p["clickCount"] = Int("clicks");
                    break;
                default: return (null, null);
            }
            return ("Input.dispatchMouseEvent", p);
        }
        if (e["key"] != null)
        {
            var name = Str("name"); var vk = Int("vk"); var mods = Int("mods");
            if (name.Length == 0) return (null, null);   // a text key: its character comes as {"text"}
            if (Str("key") == "up")
                return ("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = name, ["windowsVirtualKeyCode"] = vk, ["modifiers"] = mods });
            // Enter needs its text to act (submit, new line); other keys go raw
            return name == "Enter"
                ? ("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyDown", ["key"] = "Enter", ["code"] = "Enter", ["text"] = "\r", ["windowsVirtualKeyCode"] = 13, ["modifiers"] = mods })
                : ("Input.dispatchKeyEvent", new JsonObject { ["type"] = "rawKeyDown", ["key"] = name, ["windowsVirtualKeyCode"] = vk, ["modifiers"] = mods });
        }
        if (e["text"] != null)
            return ("Input.insertText", new JsonObject { ["text"] = Str("text") });
        if (e["nav"] != null)
            return Str("nav") switch
            {
                "back" => ("Runtime.evaluate", new JsonObject { ["expression"] = "history.back()" }),
                "forward" => ("Runtime.evaluate", new JsonObject { ["expression"] = "history.forward()" }),
                "reload" => ("Page.reload", new JsonObject()),
                "url" => ("Page.navigate", new JsonObject { ["url"] = Str("url") }),
                _ => (null, null),
            };
        return (null, null);
    }

    // ---- DevTools --------------------------------------------------------------------------------

    // Requests go to the first page (browser.Page) unless a socket is named: browser.Control speaks
    // for the whole browser (its targets and windows).
    private static Task Cdp(Browser browser, string method, JsonObject parameters, ClientWebSocket? socket = null)
        => Post(browser, Interlocked.Increment(ref browser.MessageId), method, parameters, socket);

    /// <summary>A request whose reply is wanted: the raw reply JSON, or a timeout after 2 s.</summary>
    private static async Task<string> Ask(Browser browser, string method, JsonObject parameters, ClientWebSocket? socket = null)
    {
        var id = Interlocked.Increment(ref browser.MessageId);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.Pending[id] = reply;
        try
        {
            await Post(browser, id, method, parameters, socket);
            return await reply.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { browser.Pending.TryRemove(id, out _); }
    }

    private static async Task Post(Browser browser, int id, string method, JsonObject parameters, ClientWebSocket? socket = null)
    {
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters };
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await browser.Sending.WaitAsync();
        try { await (socket ?? browser.Page!).SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { browser.Sending.Release(); }
    }

    // ---- new tabs become windows ------------------------------------------------------------------

    /// <summary>Watches the browser's pages. A page opened as a tab (Ctrl+click, a link to a new tab)
    /// lands in the only tabbed window, the desktop, and would cover the screen: it is closed there
    /// and told to the screen as <c>{"open": url}</c> — its OnWindow opens it as a window of its own.</summary>
    private static async Task Watch(Browser browser)
    {
        var buffer = new byte[1 << 16];
        var message = new MemoryStream();
        try
        {
            while (browser.Control!.State == WebSocketState.Open)
            {
                var r = await browser.Control.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (!r.EndOfMessage) continue;
                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id))
                {
                    if (browser.Pending.TryRemove(id, out var waiting)) waiting.TrySetResult(text);
                    continue;
                }
                var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                if (method is not ("Target.targetCreated" or "Target.targetInfoChanged")) continue;
                var info = root.GetProperty("params").GetProperty("targetInfo");
                var target = info.GetProperty("targetId").GetString()!;
                var url = info.GetProperty("url").GetString() ?? "";
                if (info.GetProperty("type").GetString() != "page" || target == browser.Desktop.Target
                    || url.Length == 0 || url == "about:blank") continue;
                // not awaited: it asks the browser, whose answer this loop reads
                _ = Task.Run(() => Tab(browser, target, url));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidOperationException) { }
    }

    private static async Task Tab(Browser browser, string target, string url)
    {
        if (!browser.Windows.First(target)) return;   // each page is looked at once
        try
        {
            using var reply = JsonDocument.Parse(await Ask(browser, "Browser.getWindowForTarget", new JsonObject { ["targetId"] = target }, browser.Control));
            if (reply.RootElement.GetProperty("result").GetProperty("windowId").GetInt32() != browser.DesktopWindow) return;
        }
        catch (Exception ex) when (ex is TimeoutException or JsonException or KeyNotFoundException or InvalidOperationException) { return; }
        await Cdp(browser, "Target.closeTarget", new JsonObject { ["targetId"] = target }, browser.Control);
        browser.Screen?.Display?.Tell(new JsonObject { ["open"] = url });
    }

    private static async Task Receive(Browser browser, string format, ChannelWriter<string>? frames)
    {
        var buffer = new byte[1 << 20];
        var message = new MemoryStream();
        try
        {
            while (browser.Page!.State == WebSocketState.Open)
            {
                var r = await browser.Page.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (!r.EndOfMessage) continue;
                using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                if (doc.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id)
                    && browser.Pending.TryRemove(id, out var waiting))
                {
                    waiting.TrySetResult(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                    message.SetLength(0);
                    continue;
                }
                message.SetLength(0);
                if (!doc.RootElement.TryGetProperty("method", out var m)) continue;
                if (m.GetString() == "Runtime.bindingCalled")
                {
                    // the page called plang(text). Not awaited: the goal may use this browser, whose
                    // replies this loop reads.
                    var said = doc.RootElement.GetProperty("params").GetProperty("payload").GetString() ?? "";
                    if (browser.Message is { } hear) _ = Task.Run(() => hear(said));
                    continue;
                }
                if (m.GetString() != "Page.screencastFrame" || frames == null) continue;
                var p = doc.RootElement.GetProperty("params");
                await Cdp(browser, "Page.screencastFrameAck", new JsonObject { ["sessionId"] = p.GetProperty("sessionId").GetInt32() });   // ack first
                var line = new StringBuilder(p.GetProperty("data").GetString()!.Length + 64)
                    .Append("{\"frame\":\"").Append(p.GetProperty("data").GetString())
                    .Append("\",\"format\":\"").Append(format)
                    .Append("\",\"w\":").Append(browser.Width).Append(",\"h\":").Append(browser.Height).Append('}')
                    .ToString();
                frames.TryWrite(line);
            }
        }
        catch (WebSocketException) { }
        finally { frames?.TryComplete(); }
    }

    /// <summary>The DevTools port Chromium says it took; what it says after that goes to the debug
    /// channel (<paramref name="told"/>), or nowhere.</summary>
    private static async Task<int?> PortOf(StreamReader stderr, TimeSpan within, Func<string, Task>? told = null)
    {
        using var cts = new CancellationTokenSource(within);
        try
        {
            while (await stderr.ReadLineAsync(cts.Token) is { } line)
            {
                var match = Listening().Match(line);
                if (!match.Success) continue;
                _ = Drain(stderr, told);
                return int.Parse(match.Groups[1].Value);
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    private static async Task<string?> PageOf(int port)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"));
                foreach (var target in doc.RootElement.EnumerateArray())
                    if (target.GetProperty("type").GetString() == "page")
                        return target.GetProperty("webSocketDebuggerUrl").GetString();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { }
            await Task.Delay(250);
        }
        return null;
    }

    private static async Task Drain(StreamReader reader, Func<string, Task>? told = null)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
                if (told != null) await told(line);
        }
        catch { }
    }

    private static data.@this<Browser> Fail(actor.context.@this context, string message, string key, int status)
        => data.@this<Browser>.From(context.Error(new ActionError(message, key, status)));
}
