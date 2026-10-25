using app.error;
using app.Utils;
using Bitmap = global::app.module.screen.type.screen.code.Bitmap;
using Native = global::app.module.screen.type.screen.window.code.Window;
using Text = global::app.type.item.text.@this;

namespace app.module.screen.type.screen.window;

/// <summary>
/// A screen that is a window on the host (Windows): frames drawn into it are decoded (JPEG/PNG, SkiaSharp) off the
/// window's thread and shown as BGRA pixels; a message from PlangOS's screen goes to it whole. What the person does in
/// it reaches OnInput one at a time per app (on.code.Gate), newest last — input and the clipboard as values, the rest
/// as text; moves are thinned in the window. Closing it calls OnClose.
/// </summary>
public sealed class @this : screen.@this
{
    private readonly Native _window;

    private @this(string title, int width, int height, Native window) : base(title, width, height) => _window = window;

    /// <summary>Opens a window <paramref name="width"/>×<paramref name="height"/> titled <paramref name="title"/>,
    /// centred on the main monitor: what the person does in it goes to <paramref name="onInput"/>, its closing to
    /// <paramref name="onClose"/>. Its numbers (once a second) also go to stats.jsonl in the app's folder.</summary>
    internal static global::app.data.@this<screen.@this> Start(string title, int width, int height,
        global::app.goal.step.action.@this? onInput, global::app.goal.step.action.@this? onClose, global::app.actor.context.@this context)
    {
        // what the window says: input and the clipboard as values; the rest as text, until each has its type
        var events = System.Threading.Channels.Channel.CreateUnbounded<global::app.type.item.@this>();
        var errors = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
        Text closedLine = "{\"closed\":true}";
        var window = new Native(title, width, height, context.App.type.list,
            onEvent: e => events.Writer.TryWrite(e),
            onClosed: () => { events.Writer.TryWrite(closedLine); errors.Writer.TryComplete(); },
            onError: json => errors.Writer.TryWrite(json));
        var failed = window.Show();
        if (failed != null)
            return global::app.data.@this<screen.@this>.From(context.Error(new ActionError("Could not open the screen: " + failed, "ScreenOpenFailed", 500)));
        _ = Said(events.Reader, closedLine, onInput, onClose, context);
        _ = Kept(errors.Reader, context);
        return context.Ok<screen.@this>(new @this(title, width, height, window));
    }

    // PlangOS's errors (what its desktop lists, sent up through the shell's errorlog channel), kept in errors.jsonl in
    // the app's folder — read on the host, no copying from the screen. Each a record: when, where, the error itself,
    // written by the json writer, one per line; kept across starts.
    internal static async Task Kept(System.Threading.Channels.ChannelReader<byte[]> errors, global::app.actor.context.@this context)
    {
        var file = global::app.type.item.path.file.@this.Resolve("/errors.jsonl", context);
        var json = context.App.type.list.Mime("application/json");
        await foreach (var sent in errors.ReadAllAsync())
        {
            // what came up is what the shell wrote through its json channel: read back through the json kind; what
            // doesn't read as that is kept as its text, the error itself
            var said = await json.Decode(sent, context, "error");
            var error = await said.Get("error");
            var where = await said.Get("where");
            var record = new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)])
                .Set("at", (Text)DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))
                .Set("where", where.Success ? await where.Value() : null)
                .Set("error", error.Success && await error.Value() is { IsNull: false } what ? what : (Text)System.Text.Encoding.UTF8.GetString(sent));
            var value = new global::app.data.@this("error", record, context: context);
            using var stream = new MemoryStream();
            if (!(await json.Encode(stream, value, context)).Success) continue;
            var line = System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
            await (await (await file.Exists(context)).ToBooleanAsync() ? file.Append(line, context) : file.WriteText(line, context));
        }
    }

    // what the window says, in order: its stats to stats.jsonl, an input to OnInput as itself (is input), the rest as
    // its text; the closing to OnClose, and nothing after it
    private static async Task Said(System.Threading.Channels.ChannelReader<global::app.type.item.@this> events, Text closedLine,
        global::app.goal.step.action.@this? onInput, global::app.goal.step.action.@this? onClose, global::app.actor.context.@this context)
    {
        // watched from outside (the os bot); started anew now and then, so it stays small
        const string stats = "/stats.jsonl";
        // what PlangOS's browser says of its media players (Chromium's media log), kept beside: what a video did when it stopped
        const string media = "/media.jsonl";
        var recorded = 0;
        var heard = 0;
        await foreach (var e in events.ReadAllAsync())
        {
            var said = e is Text t ? t.ToString() : null;
            var at = DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            if (said != null && said.StartsWith("{\"stats\":", StringComparison.Ordinal))
            {
                var line = "{\"at\":\"" + at[..8] + "\"," + said[1..] + "\n";
                var file = global::app.type.item.path.file.@this.Resolve(stats, context);
                await (recorded++ % 10_000 == 0 ? file.WriteText(line, context) : file.Append(line, context));
            }
            else if (said != null && said.StartsWith("{\"guest\":{\"media\":", StringComparison.Ordinal))
            {
                var line = "{\"at\":\"" + at + "\"," + said[1..] + "\n";
                var file = global::app.type.item.path.file.@this.Resolve(media, context);
                await (heard++ % 50_000 == 0 ? file.WriteText(line, context) : file.Append(line, context));
            }
            var closed = ReferenceEquals(e, closedLine);
            var call = closed ? onClose : onInput;
            if (call != null)
                await global::app.module.on.code.Gate.Call(call, new global::app.data.@this("!data", e, context: context), context);
            if (closed) break;
        }
    }

    private protected override bool IsClosed => _window.Closed;

    private protected override int Drawn => _window.Frames;

    internal override bool Show(byte[] message) => !_window.Closed && _window.Take(message);

    internal override async Task<global::app.data.@this> Draw(global::app.data.@this frame, global::app.actor.context.@this context)
    {
        if (_window.Closed) return context.Ok();   // closed: nothing to draw on

        var value = await frame.Value();
        if (value is Text t)
        {
            // A line from a browser: a frame (decoded by the window's own thread, newest only — this
            // step returns at once, so input behind it never waits) or the pointer the page wants.
            var line = t.Clr<string>() ?? "";
            if (line.StartsWith("{\"rect", StringComparison.Ordinal)) _window.Patch(line);   // {"rects":…} or {"rect":…}
            else if (line.Contains("\"frame\":\"", StringComparison.Ordinal)) _window.Offer(line, offered => Present(offered, context));
            else if (Field(line, "cursor") is { } css) _window.Cursor(css);
            return context.Ok();
        }

        if (value?.RawBytes is not { } encoded) return context.Ok();   // not a frame: ignored

        // a message from PlangOS's screen: the window knows what it means
        if (_window.Take(encoded)) return context.Ok();

        try
        {
            _window.Present(new Bitmap(encoded));
        }
        catch (AppException ex)
        {
            return context.Error(ex.Error);
        }
        return context.Ok();
    }

    /// <summary>A window that shows frames takes no input lines: its input comes out of it (OnInput).</summary>
    internal override Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context)
        => Task.FromResult(context.Error(new ActionError("This screen shows frames; it takes no input. screen.send is PlangOS's display (Linux).", "NotSupported", 400)));

    internal override void Close() => _window.Close();

    // a browser's frame line, on the window's decode thread: its picture shown, or, when it is none, the error
    // reported (no step waits for it to answer)
    private void Present(string line, global::app.actor.context.@this context)
    {
        try
        {
            _window.Present(new Bitmap(Frame(line)));
        }
        catch (AppException ex)
        {
            _ = context.Actor.Channel.Report(context.Error(ex.Error));
        }
    }

    // "key":"value" from a one-line JSON message, without parsing a 200 KB frame line
    private static string? Field(string line, string key)
    {
        var marker = "\"" + key + "\":\"";
        var start = line.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = line.IndexOf('"', start);
        return end < 0 ? null : line[start..end];
    }

    // {"frame":"<base64>",...} → the image bytes; a line without them is an AppException carrying BadFrame
    private static byte[] Frame(string line)
    {
        if (Field(line, "frame") is not { } base64) throw new AppException(new Error("Not an image: the line holds no frame", "BadFrame", 400));
        try { return Convert.FromBase64String(base64); }
        catch (FormatException ex)
        {
            var error = new Error("Not an image: the frame isn't base64", "BadFrame", 400);
            error.list.Add(Error.FromException(ex));
            throw new AppException(error, ex);
        }
    }
}
