using app.error;
using app.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Native = global::app.module.screen.type.screen.window.code.Window;
using Text = global::app.type.item.text.@this;

namespace app.module.screen.type.screen.window;

/// <summary>
/// A screen that is a window on the host (Windows): frames drawn into it are decoded (JPEG/PNG, ImageSharp) off the
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
        Text closedLine = "{\"closed\":true}";
        var window = new Native(title, width, height,
            onEvent: e => events.Writer.TryWrite(e),
            onClosed: () => events.Writer.TryWrite(closedLine));
        var failed = window.Show();
        if (failed != null)
            return global::app.data.@this<screen.@this>.From(context.Error(new ActionError("Could not open the screen: " + failed, "ScreenOpenFailed", 500)));
        _ = Said(events.Reader, closedLine, onInput, onClose, context);
        return context.Ok<screen.@this>(new @this(title, width, height, window));
    }

    // what the window says, in order: its stats to stats.jsonl, an input to OnInput as itself (is input), the rest as
    // its text; the closing to OnClose, and nothing after it
    private static async Task Said(System.Threading.Channels.ChannelReader<global::app.type.item.@this> events, Text closedLine,
        global::app.goal.step.action.@this? onInput, global::app.goal.step.action.@this? onClose, global::app.actor.context.@this context)
    {
        // watched from outside (the os bot); started anew now and then, so it stays small
        var stats = PathHelper.Combine(context.App.AbsolutePath, "stats.jsonl");
        var recorded = 0;
        await foreach (var e in events.ReadAllAsync())
        {
            var said = e is Text t ? t.ToString() : null;
            if (said != null && said.StartsWith("{\"stats\":", StringComparison.Ordinal))
            {
                var line = "{\"at\":\"" + DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\"," + said[1..] + "\n";
                var file = global::app.type.item.path.file.@this.Resolve(stats, context);
                await (recorded++ % 10_000 == 0 ? file.WriteText(line, context) : file.Append(line, context));
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
            else if (line.Contains("\"frame\":\"", StringComparison.Ordinal)) _window.Offer(line, Decode);
            else if (Field(line, "cursor") is { } css) _window.Cursor(css);
            return context.Ok();
        }

        if (value?.RawBytes is not { } encoded) return context.Ok();   // not a frame: ignored

        // a message from PlangOS's screen: the window knows what it means
        if (_window.Take(encoded)) return context.Ok();

        try
        {
            if (Pixels(encoded) is { } px) _window.Present(px.bgra, px.w, px.h);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return context.Error(new ActionError("Not an image: " + ex.Message, "BadFrame", 400));
        }
        return context.Ok();
    }

    /// <summary>A window that shows frames takes no input lines: its input comes out of it (OnInput).</summary>
    internal override Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context)
        => Task.FromResult(context.Error(new ActionError("This screen shows frames; it takes no input. screen.send is PlangOS's display (Linux).", "NotSupported", 400)));

    internal override void Close() => _window.Close();

    private static (byte[] bgra, int w, int h)? Decode(string line)
        => Frame(line) is { } encoded ? Pixels(encoded) : null;

    private static (byte[] bgra, int w, int h)? Pixels(byte[] encoded)
    {
        using var image = Image.Load<Bgra32>(encoded);
        var bgra = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bgra);
        return (bgra, image.Width, image.Height);
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

    // {"frame":"<base64>",...} → the image bytes; anything else → null
    private static byte[]? Frame(string? line)
    {
        if (line == null) return null;
        const string key = "\"frame\":\"";
        var start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) return null;
        start += key.Length;
        var end = line.IndexOf('"', start);
        if (end < 0) return null;
        try { return Convert.FromBase64String(line[start..end]); }
        catch (FormatException) { return null; }
    }
}
