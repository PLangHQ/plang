using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using FilePath = global::app.type.item.path.file.@this;
using Text = global::app.type.item.text.@this;

namespace app.module.browser.type.browser.headless;

/// <summary>
/// A browser that renders one page off-screen (no window): frames come from <c>Page.startScreencast</c>, each acked at
/// once so Chromium keeps sending, and only the newest goes to OnFrame — a slow OnFrame skips frames rather than
/// falling behind. Input goes in through <c>Input.dispatch*</c>; after a move it asks the page what is under the
/// pointer and sends <c>{"cursor":…}</c> up beside the frames when that changes.
/// </summary>
public sealed class @this : browser.@this
{
    // Headless Chromium says "HeadlessChrome" in its user agent; bot checks (Cloudflare) stop there.
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0.0.0 Safari/537.36";

    private readonly ClientWebSocket _page;
    private readonly string _format;
    // sends a line up beside the frames (the pointer the page wants)
    private Func<string, Task>? _emit;
    private string _lastCursor = "default";
    private long _lastCursorAsk;
    private int _cursorBusy;

    private @this(string url, int width, int height, Process os, ClientWebSocket page, string format)
        : base(url, width, height, os)
    {
        _page = page;
        _format = format;
    }

    private protected override string Variant => "headless";

    private protected override ClientWebSocket? Speaks => _page;

    /// <summary>Starts <paramref name="chromium"/> headless on <paramref name="url"/>, frames
    /// <paramref name="width"/>×<paramref name="height"/> in <paramref name="format"/> to <paramref name="onFrame"/>.</summary>
    internal static async Task<global::app.data.@this<browser.@this>> Start(FilePath chromium, string url, int width, int height,
        string format, int quality, global::app.goal.step.action.@this? onFrame, global::app.actor.context.@this context)
    {
        var (profile, refused) = await Profile(context);
        if (profile == null) return global::app.data.@this<browser.@this>.From(refused!);

        var info = new ProcessStartInfo(chromium.Absolute)
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
            $"--user-data-dir={profile.Absolute}",
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

        var page = new ClientWebSocket();
        page.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        await page.ConnectAsync(new Uri(pageUrl), CancellationToken.None);
        var browser = new @this(url, width, height, os, page, format);
        await browser.Cdp("Page.enable", new JsonObject());
        await browser.Cdp("Page.startScreencast", new JsonObject
        {
            ["format"] = format, ["quality"] = quality, ["maxWidth"] = width, ["maxHeight"] = height, ["everyNthFrame"] = 1,
        });

        // Newest frame only: a slow OnFrame skips frames rather than falling behind.
        var frames = System.Threading.Channels.Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        _ = Task.Run(() => browser.Receive(frames.Writer));
        if (onFrame != null)
        {
            Task Deliver(string line) => global::app.module.on.code.Gate.Call(onFrame,
                new global::app.data.@this("!data", line, context.App.type.list["text"], context: context), context);
            browser._emit = Deliver;
            _ = Task.Run(async () =>
            {
                await foreach (var line in frames.Reader.ReadAllAsync()) await Deliver(line);
            });
        }
        return context.Ok<browser.@this>(browser);
    }

    internal override async Task<global::app.data.@this> Send(global::app.data.@this data, global::app.actor.context.@this context)
    {
        if (_page.State != WebSocketState.Open)
            return context.Error(new global::app.error.ActionError($"The browser isn't running: {this}", "BrowserNotRunning", 409));
        var value = await data.Value();
        var line = value is Text t ? t.Clr<string>() : value?.ToString();
        if (string.IsNullOrWhiteSpace(line)) return context.Ok();

        JsonNode? input;
        try { input = JsonNode.Parse(line); }
        catch (JsonException) { return context.Ok(); }   // not an input event: ignored
        if (input is not JsonObject e) return context.Ok();

        var (method, parameters) = Translate(e);
        if (method == null) return context.Ok();
        await Cdp(method, parameters!);
        if (method == "Input.dispatchMouseEvent" && parameters!["type"]?.GetValue<string>() == "mouseMoved")
            AskCursor(parameters["x"]!.GetValue<int>(), parameters["y"]!.GetValue<int>());
        return context.Ok();
    }

    // The frames don't carry the pointer. After a move (at most 20 times a second, one question at a
    // time) ask the page what is under the mouse and send {"cursor":…} up when it changes. Not awaited:
    // this runs inside the input call, and the answer goes out through the same one-at-a-time gate.
    private void AskCursor(int x, int y)
    {
        var now = Environment.TickCount64;
        if (_emit == null || now - _lastCursorAsk < 50) return;
        if (Interlocked.CompareExchange(ref _cursorBusy, 1, 0) != 0) return;
        _lastCursorAsk = now;
        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await Ask("Runtime.evaluate", new JsonObject
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
                if (cursor == _lastCursor) return;
                _lastCursor = cursor;
                await _emit!(new JsonObject { ["cursor"] = cursor }.ToJsonString());
            }
            catch (Exception ex) when (ex is TimeoutException or JsonException or WebSocketException or InvalidOperationException)
            {
                // a lost answer only means the pointer stays as it was
            }
            finally { Interlocked.Exchange(ref _cursorBusy, 0); }
        });
    }

    // the neutral event lines → DevTools
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

    // the page's messages: replies to what was asked, and the frames
    private async Task Receive(ChannelWriter<string> frames)
    {
        var buffer = new byte[1 << 20];
        var message = new MemoryStream();
        try
        {
            while (_page.State == WebSocketState.Open)
            {
                var r = await _page.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (!r.EndOfMessage) continue;
                using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                if (doc.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id)
                    && Answered(id, Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length)))
                {
                    message.SetLength(0);
                    continue;
                }
                message.SetLength(0);
                if (!doc.RootElement.TryGetProperty("method", out var m) || m.GetString() != "Page.screencastFrame") continue;
                var p = doc.RootElement.GetProperty("params");
                await Cdp("Page.screencastFrameAck", new JsonObject { ["sessionId"] = p.GetProperty("sessionId").GetInt32() });   // ack first
                var line = new StringBuilder(p.GetProperty("data").GetString()!.Length + 64)
                    .Append("{\"frame\":\"").Append(p.GetProperty("data").GetString())
                    .Append("\",\"format\":\"").Append(_format)
                    .Append("\",\"w\":").Append(Width.ToInt32()).Append(",\"h\":").Append(Height.ToInt32()).Append('}')
                    .ToString();
                frames.TryWrite(line);
            }
        }
        catch (WebSocketException) { }
        finally { frames.TryComplete(); }
    }
}
