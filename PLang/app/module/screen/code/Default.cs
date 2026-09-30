using app.error;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Text = app.type.item.text.@this;

namespace app.module.screen.code;

/// <summary>
/// Default screen provider: a Win32 window (Windows). Frames are decoded (JPEG/PNG, ImageSharp)
/// here, off the window's thread, and handed to it as BGRA pixels. Input events from the window
/// reach OnInput one at a time per app (on.code.Gate), newest last; moves are thinned in the window.
/// </summary>
public sealed class Default : IScreen
{
    public string Name { get; init; } = "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<Screen>> Open(open action)
    {
        var context = action.Context;
        if (!OperatingSystem.IsWindows())
            return data.@this<Screen>.From(context.Error(new ActionError("screen.open needs Windows for now.", "NotSupported", 400)));

        var title = (await action.Title.Value())!.Clr<string>()!;
        var width = (int)(await action.Width.Value())!.ToDouble();
        var height = (int)(await action.Height.Value())!.ToDouble();
        var onInput = action.OnInput == null ? null : await action.OnInput.Value();
        var onClose = action.OnClose == null ? null : await action.OnClose.Value();

        var events = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var window = new Window(title, width, height,
            onEvent: e => events.Writer.TryWrite(e),
            onClosed: () => events.Writer.TryWrite("{\"closed\":true}"));
        var failed = window.Show();
        if (failed != null)
            return data.@this<Screen>.From(context.Error(new ActionError("Could not open the screen: " + failed, "ScreenOpenFailed", 500)));

        _ = Task.Run(async () =>
        {
            await foreach (var e in events.Reader.ReadAllAsync())
            {
                var closed = e == "{\"closed\":true}";
                var call = closed ? onClose : onInput;
                if (call != null)
                    await global::app.module.on.code.Gate.Call(call,
                        new data.@this("!data", e, context.App.type.list["text"], context: context), context);
                if (closed) break;
            }
        });
        return context.Ok<Screen>(new Screen { Title = title, Width = width, Height = height, Window = window });
    }

    public async Task<data.@this> Draw(draw action)
    {
        var context = action.Context;
        var screen = await action.Screen.Value();
        if (screen?.Window is not { Closed: false } window) return context.Ok();   // closed: nothing to draw on

        var value = await action.Data.Value();
        if (value is Text t)
        {
            // A line from a browser: a frame (decoded by the window's own thread, newest only — this
            // step returns at once, so input behind it never waits) or the pointer the page wants.
            var line = t.Clr<string>() ?? "";
            if (line.StartsWith("{\"rect", StringComparison.Ordinal)) window.Patch(line);   // {"rects":…} or {"rect":…}
            else if (line.Contains("\"frame\":\"", StringComparison.Ordinal)) window.Offer(line, Decode);
            else if (Field(line, "cursor") is { } css) window.Cursor(css);
            return context.Ok();
        }

        if (value?.RawBytes is not { } encoded) return context.Ok();   // not a frame: ignored

        // a message from PlangOS's screen: the window knows what it means
        if (window.Take(encoded)) return context.Ok();

        try
        {
            if (Pixels(encoded) is { } px) window.Present(px.bgra, px.w, px.h);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return context.Error(new ActionError("Not an image: " + ex.Message, "BadFrame", 400));
        }
        return context.Ok();
    }

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

    /// <summary>A window that shows frames takes no input lines: its input comes out of it (OnInput).</summary>
    public Task<data.@this> Send(send action)
        => Task.FromResult(action.Context.Error(new ActionError("This screen shows frames; it takes no input. screen.send is PlangOS's display (Linux).", "NotSupported", 400)));

    public async Task<data.@this> Close(close action)
    {
        var screen = await action.Screen.Value();
        screen?.Window?.Close();
        return action.Context.Ok();
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
