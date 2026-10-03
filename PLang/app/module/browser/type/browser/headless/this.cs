using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Program = global::app.module.terminal.Process;

namespace app.module.browser.type.browser.headless;

/// <summary>
/// A browser that renders one page off-screen (no window): frames come from <c>Page.startScreencast</c>, each acked at
/// once so Chromium keeps sending, and only the newest goes to OnFrame — a slow OnFrame skips frames rather than
/// falling behind. Input goes in through <c>Input.dispatch*</c>; after a move it asks the page what is under the
/// pointer and sends its <see cref="cursor.@this"/> up beside the frames when that changes. Chromium is started by
/// <c>/system/browser/Headless</c>; the page and its size are given over DevTools.
/// </summary>
public sealed class @this : browser.@this
{
    private readonly cdp.session.@this _page;
    private readonly string _format;
    // sends a line up beside the frames (the pointer the page wants)
    private Func<string, Task>? _emit;
    private string _lastCursor = "default";
    private long _lastCursorAsk;
    private int _cursorBusy;

    private @this(string url, int width, int height, Program program, cdp.@this cdp, cdp.session.@this page, string format)
        : base(url, width, height, program, cdp)
    {
        _page = page;
        _format = format;
    }

    private protected override string Variant => "headless";

    /// <summary>Starts headless Chromium on <paramref name="url"/>, frames <paramref name="width"/>×<paramref name="height"/>
    /// in <paramref name="format"/> to <paramref name="onFrame"/>.</summary>
    internal static async Task<global::app.data.@this<browser.@this>> Start(string url, int width, int height,
        string format, int quality, global::app.goal.step.action.@this? onFrame, global::app.actor.context.@this context)
    {
        if (await Readable(url, context) is { } refused) return global::app.data.@this<browser.@this>.From(refused);
        var (program, failed) = await Started("Headless", context);
        if (program == null) return global::app.data.@this<browser.@this>.From(failed!);
        var cdp = new cdp.@this(program.pipe!);
        try
        {
            var target = await FirstPage(cdp);
            var page = await cdp.Attach(target);
            var browser = new @this(url, width, height, program, cdp, page, format);
            await page.Ask("Page.enable", new JsonObject());
            await page.Ask("Emulation.setDeviceMetricsOverride", new JsonObject
            {
                ["width"] = width, ["height"] = height, ["deviceScaleFactor"] = 1, ["mobile"] = false,
            });
            await page.Ask("Page.navigate", new JsonObject { ["url"] = url });

            // Newest frame only: a slow OnFrame skips frames rather than falling behind.
            var frames = System.Threading.Channels.Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
            page.Heard += (method, parameters) => method == "Page.screencastFrame" ? browser.Framed(parameters, frames.Writer) : Task.CompletedTask;
            await page.Ask("Page.startScreencast", new JsonObject
            {
                ["format"] = format, ["quality"] = quality, ["maxWidth"] = width, ["maxHeight"] = height, ["everyNthFrame"] = 1,
            });
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
            browser.Watched();
            return context.Ok<browser.@this>(browser);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException)
        {
            program.Os?.Kill();
            return Fail(context, $"Chromium didn't open its page: {ex.Message}", "BrowserStartFailed", 500);
        }
    }

    // the page Chromium opened (about:blank), once DevTools lists it
    private static async Task<string> FirstPage(cdp.@this cdp)
    {
        for (var tries = 0; tries < 120; tries++)
        {
            if ((await cdp.Pages()).FirstOrDefault() is { target: { } target }) return target;
            await Task.Delay(250);
        }
        throw new TimeoutException("Chromium has no page to show");
    }

    // a frame: acked first (Chromium sends the next only then), then the newest goes on
    private async Task Framed(JsonElement parameters, ChannelWriter<string> frames)
    {
        await _page.Tell("Page.screencastFrameAck", new JsonObject { ["sessionId"] = parameters.GetProperty("sessionId").GetInt32() });
        var data = parameters.GetProperty("data").GetString()!;
        frames.TryWrite(new StringBuilder(data.Length + 64)
            .Append("{\"frame\":\"").Append(data)
            .Append("\",\"format\":\"").Append(_format)
            .Append("\",\"w\":").Append(Width.ToInt32()).Append(",\"h\":").Append(Height.ToInt32()).Append('}')
            .ToString());
    }

    internal override async Task<global::app.data.@this> Send(global::app.type.item.input.@this input, global::app.actor.context.@this context)
    {
        if (!Running.Value)
            return context.Error(new global::app.error.ActionError($"The browser isn't running: {this}", "BrowserNotRunning", 409));
        var asked = new Asked();
        input.Apply(asked);
        if (asked.Method == null) return context.Ok();   // a key that types text comes as its text
        await _page.Tell(asked.Method, asked.Parameters!);
        if (asked.Moved is { } at) AskCursor(at.x, at.y, context);
        return context.Ok();
    }

    /// <summary>What an input asks of DevTools: each variant, handed over whole, lowered to its request — the
    /// browser's native edge.</summary>
    internal sealed class Asked : global::app.type.item.input.ITarget
    {
        internal string? Method { get; private set; }
        internal JsonObject? Parameters { get; private set; }
        /// <summary>Where the mouse moved to, when it did: the pointer there is asked for.</summary>
        internal (int x, int y)? Moved { get; private set; }

        // the modifier keys by name → DevTools' bits (alt 1, ctrl 2, meta 4, shift 8: the modifier choice's own values)
        private static int Bits(global::app.type.item.list.@this<global::app.type.item.choice.@this<global::app.type.item.input.Modifier>> held)
            => held.Items().Aggregate(0, (bits, modifier) => bits | (int)modifier.Value);

        public void Stamped(long stamp) { }

        public void Mouse(global::app.type.item.input.mouse.@this mouse)
        {
            int x = mouse.X.Clr<int>(), y = mouse.Y.Clr<int>();
            var p = new JsonObject { ["x"] = x, ["y"] = y, ["modifiers"] = Bits(mouse.Modifiers) };
            switch (mouse.Action.Value)
            {
                case global::app.type.item.input.mouse.Gesture.wheel:
                    p["type"] = "mouseWheel"; p["deltaX"] = mouse.Dx.Clr<int>(); p["deltaY"] = mouse.Dy.Clr<int>();
                    break;
                case var gesture:
                    p["type"] = gesture switch
                    {
                        global::app.type.item.input.mouse.Gesture.down => "mousePressed",
                        global::app.type.item.input.mouse.Gesture.up => "mouseReleased",
                        _ => "mouseMoved",
                    };
                    p["button"] = mouse.Button.Value.ToString();
                    p["clickCount"] = mouse.Clicks.Clr<int>();
                    if (gesture == global::app.type.item.input.mouse.Gesture.move) Moved = (x, y);
                    break;
            }
            (Method, Parameters) = ("Input.dispatchMouseEvent", p);
        }

        public void Key(global::app.type.item.input.key.@this key)
        {
            if (key.Name?.ToString() is not { Length: > 0 } name) return;   // a text key: its character comes as text
            int vk = key.Vk.Clr<int>(), mods = Bits(key.Modifiers);
            Method = "Input.dispatchKeyEvent";
            Parameters = !key.Down.Value
                ? new JsonObject { ["type"] = "keyUp", ["key"] = name, ["windowsVirtualKeyCode"] = vk, ["modifiers"] = mods }
                // Enter needs its text to act (submit, new line); other keys go raw
                : name == "Enter"
                    ? new JsonObject { ["type"] = "keyDown", ["key"] = "Enter", ["code"] = "Enter", ["text"] = "\r", ["windowsVirtualKeyCode"] = 13, ["modifiers"] = mods }
                    : new JsonObject { ["type"] = "rawKeyDown", ["key"] = name, ["windowsVirtualKeyCode"] = vk, ["modifiers"] = mods };
        }

        public void Text(global::app.type.item.input.text.@this text)
            => (Method, Parameters) = ("Input.insertText", new JsonObject { ["text"] = text.Typed.ToString() });

        public void Navigate(global::app.type.item.input.navigate.@this navigate)
            => (Method, Parameters) = navigate.To.Value switch
            {
                global::app.type.item.input.navigate.Direction.back => ("Runtime.evaluate", new JsonObject { ["expression"] = "history.back()" }),
                global::app.type.item.input.navigate.Direction.forward => ("Runtime.evaluate", new JsonObject { ["expression"] = "history.forward()" }),
                _ => ("Page.reload", new JsonObject()),
            };
    }

    // The frames don't carry the pointer. After a move (at most 20 times a second, one question at a
    // time) ask the page what is under the mouse and send its cursor up when it changes. Not awaited:
    // this runs inside the input call, and the answer goes out through the same one-at-a-time gate.
    private void AskCursor(int x, int y, global::app.actor.context.@this context)
    {
        var now = Environment.TickCount64;
        if (_emit == null || now - _lastCursorAsk < 50) return;
        if (Interlocked.CompareExchange(ref _cursorBusy, 1, 0) != 0) return;
        _lastCursorAsk = now;
        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await _page.Ask("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "(function(){var e=document.elementFromPoint(" + x + "," + y + ");if(!e)return 'default';" +
                        "var c=getComputedStyle(e).cursor;if(c!=='auto')return c;if(e.closest('a[href],button,[role=button],label'))return 'pointer';" +
                        "if(e.isContentEditable||/^(INPUT|TEXTAREA)$/.test(e.tagName))return 'text';return 'default'})()",
                    ["returnByValue"] = true,
                });
                if (!reply.TryGetProperty("result", out var r) || !r.TryGetProperty("result", out var v)
                    || !v.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return;
                var css = value.GetString()!;
                if (css == _lastCursor) return;
                _lastCursor = css;
                // the cursor writes itself: the line it is, beside the frames
                using var written = new MemoryStream();
                var encoded = await context.App.type.list.Mime("application/json").Encode(written, context.Ok(new cursor.@this(css)), context);
                if (encoded.Success) await _emit!(Encoding.UTF8.GetString(written.ToArray()));
            }
            catch (Exception ex) when (ex is TimeoutException or JsonException or IOException or InvalidOperationException)
            {
                // a lost answer only means the pointer stays as it was
            }
            finally { Interlocked.Exchange(ref _cursorBusy, 0); }
        });
    }
}
