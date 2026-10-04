using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Shown = global::app.module.screen.type.screen.code.Shown;
using Text = global::app.type.item.text.@this;

namespace app.module.screen.type.screen.view;

/// <summary>
/// A screen on a Linux host that shows PlangOS's frames without a window: what the Windows host's window does with
/// them — the frames applied to its picture, a video's pictures decoded (openh264), the input's echoes timed, its
/// numbers once a second — kept in memory, its picture read as an image (<c>%screen.picture%</c>). What the person
/// does comes from this app's input instead of a window: one JSON line each, as the window would give it
/// (<c>{"mouse":…}</c>, <c>{"key":…}</c>, <c>{"text":…}</c>, <c>{"nav":…}</c>); it reaches OnInput as itself, and its
/// end closes the screen (OnClose). So a Linux machine can run PlangOS as the Windows host does — and test it.
/// </summary>
public sealed class @this : screen.@this
{
    private readonly Shown _shown;
    private readonly BlockingCollection<byte[]> _messages = new();
    private readonly Func<global::app.type.item.@this, Task> _said;
    private readonly Func<Task> _closing;
    private code.Video? _video;
    private bool _noVideo;
    private readonly code.Media _media = new();
    private int _told;   // PlangOS told the codecs this host decodes (once it sends frames, it listens)
    private volatile bool _closed;
    private int _frames;

    private @this(string title, int width, int height, Func<global::app.type.item.@this, Task> said, Func<Task> closing)
        : base(title, width, height)
    {
        _said = said;
        _closing = closing;
        _shown = new Shown(width, height, Decoded);
        new Thread(Apply) { IsBackground = true, Name = "screen view: " + title }.Start();
    }

    /// <summary>Opens a view <paramref name="width"/>×<paramref name="height"/>: what PlangOS sends is shown in it, what
    /// it says (input, a message from PlangOS, its numbers) goes to <paramref name="onInput"/>, its end to
    /// <paramref name="onClose"/>.</summary>
    internal static global::app.data.@this<screen.@this> Start(string title, int width, int height,
        global::app.goal.step.action.@this? onInput, global::app.goal.step.action.@this? onClose, global::app.actor.context.@this context)
    {
        Task Call(global::app.goal.step.action.@this? call, global::app.type.item.@this? value)
            => call == null ? Task.CompletedTask
                : global::app.module.on.code.Gate.Call(call, new global::app.data.@this("!data", value, context: context), context);
        var view = new @this(title, width, height, value => Call(onInput, value), () => Call(onClose, (Text)"{\"closed\":true}"));
        return context.Ok<screen.@this>(view);
    }

    /// <summary>The picture it shows now, as a PNG image.</summary>
    [global::app.LlmBuilder, global::app.Out]
    public global::app.type.item.image.@this Picture
    {
        get
        {
            byte[] copy;
            lock (_shown.Lock) copy = (byte[])_shown.Pixels.Clone();
            _media.Draw(copy, _shown.Width, _shown.Height);   // the videos it plays itself, where the page shows them
            using var image = Image.LoadPixelData<Bgra32>(copy, _shown.Width, _shown.Height);
            using var png = new MemoryStream();
            image.SaveAsPng(png);
            return new global::app.type.item.image.@this(png.ToArray(), "image/png");
        }
    }

    private protected override bool IsClosed => _closed;
    private protected override int Drawn => _frames;

    /// <summary>A message from PlangOS's screen: frames and echoes in order (their own thread), a message from its
    /// PLang to OnInput; the pointer and copied text mean nothing without a window.</summary>
    internal override bool Show(byte[] message)
    {
        if (_closed || message.Length == 0) return false;
        if (Interlocked.Exchange(ref _told, 1) == 0 && code.Media.Codecs is { Length: > 0 } codecs)
            _ = _said((Text)("{\"codecs\":" + System.Text.Json.JsonSerializer.Serialize(codecs) + "}"));
        switch (message[0])
        {
            case 1 or 3 or 4 or 7: _messages.Add(message); return true;
            case >= 10 and <= 13: return _media.Take(message);
            case 9: _ = _said((Text)("{\"guest\":" + System.Text.Encoding.UTF8.GetString(message, 1, message.Length - 1) + "}")); return true;
            case 2 or 5: return true;
            default: return false;
        }
    }

    internal override Task<global::app.data.@this> Draw(global::app.data.@this frame, global::app.actor.context.@this context)
        => Task.FromResult(frame.Peek()?.RawBytes is { } bytes && Show(bytes) ? context.Ok()
            : context.Error(new global::app.error.ActionError("A view shows what PlangOS's screen sends, nothing else.", "BadFrame", 400)));

    internal override Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context)
        => Task.FromResult(context.Error(new global::app.error.ActionError("A view shows frames; its input comes from this app's input (screen.listen).", "NotSupported", 400)));

    /// <summary>This app's input, line by line, as the person's: an input or a clipboard to OnInput as itself, any
    /// other line as its text; its end closes the view.</summary>
    internal override async Task<global::app.data.@this> Listen(global::app.actor.context.@this context)
    {
        var lines = 0;
        using var reader = new StreamReader(Console.OpenStandardInput(), new System.Text.UTF8Encoding(false), false, 1 << 16);
        while (!_closed && await reader.ReadLineAsync() is { } line)
        {
            lines++;
            global::app.type.item.@this? value;
            try { value = Value(line, context); }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or InvalidOperationException)
            {
                await context.Actor.Channel.Report(context.Error(new global::app.error.ActionError(
                    $"An input line the view can't read: {ex.Message} — {(line.Length > 200 ? line[..200] + "…" : line)}", "InputInvalid", 400)));
                continue;
            }
            await _said(value ?? (Text)line);
        }
        Close();
        await _closing();
        return context.Ok<global::app.type.item.number.@this>(lines);
    }

    internal override void Close()
    {
        if (_closed) return;
        _closed = true;
        _messages.CompleteAdding();
        _media.Dispose();
    }

    /// <summary>Video pictures it has shown itself (pass-through) — <c>%screen.videoframes%</c>.</summary>
    [global::app.LlmBuilder, global::app.Out]
    public global::app.type.item.number.@this VideoFrames => _media.Shown;

    /// <summary>What its videos have done so far, as text: samples, decoded, restarts, why nothing new showed.</summary>
    [global::app.LlmBuilder, global::app.Out]
    public global::app.type.item.text.@this VideoNumbers => _media.Numbers;

    // ---- what PlangOS sends, in order --------------------------------------------------------------

    private long _bytes, _updates, _ticks, _since = Environment.TickCount64;
    private readonly long[] _picture = new long[64], _pipe = new long[64];
    private int _pictures, _pipes;

    private void Apply()
    {
        foreach (var m in _messages.GetConsumingEnumerable())
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (m.Length == 9 && m[0] is 3 or 4)
            {
                // 3: the frames before it are shown — now minus its stamp is input → picture; 4: echoed on arrival —
                // the pipe's round trip (the stamp is the input's, Environment.TickCount64 where it was made)
                var ms = Environment.TickCount64 - BitConverter.ToInt64(m, 1);
                if (m[0] == 3) _picture[_pictures++ % 64] = ms; else _pipe[_pipes++ % 64] = ms;
                continue;
            }
            try
            {
                if (_shown.Apply(m) != null) Interlocked.Increment(ref _frames);
            }
            catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException)
            {
                // a bad message is skipped; what follows puts the picture right
            }
            _bytes += m.Length;
            Stats(System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }
        _video?.Dispose();
    }

    // a video's next picture: a new stream number is a new stream; no openh264 here, and videos stay lossless
    private byte[]? Decoded(uint id, ReadOnlyMemory<byte> h264, int width, int height)
    {
        if (_noVideo) return null;
        try
        {
            if (_video?.Id != id)
            {
                _video?.Dispose();
                _video = new code.Video(id);
            }
            return _video.Next(h264, width, height);
        }
        catch (InvalidOperationException ex)
        {
            _noVideo = true;
            _ = _said((Text)("{\"video\":false,\"why\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}"));
            return null;
        }
    }

    // once a second, the numbers PlangOS shows on its taskbar (the Windows window's): updates/s, MB/s, ms to apply,
    // what waits, input → picture and the pipe's round trip
    private void Stats(long applied)
    {
        _updates++;
        _ticks += applied;
        var now = Environment.TickCount64;
        if (now - _since < 1000) return;
        var seconds = (now - _since) / 1000.0;
        var ms = _ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _updates;
        var numbers = System.Globalization.CultureInfo.InvariantCulture;
        var stats = "{\"stats\":{" + string.Format(numbers, "\"updates\":{0:F0},\"mb\":{1:F2},\"apply\":{2:F1},\"queued\":{3}",
            _updates / seconds, _bytes / 1048576.0 / seconds, ms, _messages.Count)
            + ",\"picture\":" + Percentiles(_picture, _pictures) + ",\"pipe\":" + Percentiles(_pipe, _pipes) + "}}";
        _ = _said((Text)stats);
        _updates = 0; _ticks = 0; _bytes = 0; _since = now;
    }

    private static string Percentiles(long[] values, int count)
    {
        var n = Math.Min(count, values.Length);
        if (n == 0) return "{\"p50\":-1,\"p85\":-1,\"p95\":-1}";
        var sorted = values.Take(n).Order().ToArray();
        return $"{{\"p50\":{sorted[n * 50 / 100]},\"p85\":{sorted[Math.Min(n - 1, n * 85 / 100)]},\"p95\":{sorted[Math.Min(n - 1, n * 95 / 100)]}}}";
    }
}
