using System.Runtime.InteropServices;
using System.Text.Json;

namespace app.module.screen.code;

/// <summary>
/// A plain Win32 window on its own thread: frames are BGRA pixels blitted with SetDIBitsToDevice;
/// mouse and keyboard become JSON lines (the neutral input events browser.send takes). No WinForms —
/// PLang targets net10.0, not net10.0-windows.
/// </summary>
internal sealed class Window
{
    private readonly string title;
    private readonly int width, height;
    // what happens here goes to plang as values: input and the clipboard as themselves; the rest (a message from
    // PlangOS, video, the window's numbers) as its text until it has a type of its own
    private readonly Action<global::app.type.item.@this> onEvent;
    private readonly Action onClosed;
    private readonly WndProc proc;          // held: the OS calls it for the window's whole life
    private readonly ManualResetEventSlim ready = new();
    private readonly object gate = new();
    private byte[] pixels;
    private int frameWidth, frameHeight;
    private IntPtr hwnd;
    private long lastMove;
    private string? error;
    private static int windows;

    public bool Closed { get; private set; }
    public int Frames;

    public Window(string title, int width, int height, Action<global::app.type.item.@this> onEvent, Action onClosed)
    {
        this.title = title; this.width = width; this.height = height;
        this.onEvent = onEvent; this.onClosed = onClosed;
        pixels = new byte[width * height * 4];
        frameWidth = width; frameHeight = height;
        proc = Proc;
    }

    /// <summary>Creates the window on its own thread; returns when it is on screen (or why not).</summary>
    public string? Show()
    {
        var thread = new Thread(Run) { IsBackground = true, Name = "screen: " + title };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return error;
    }

    /// <summary>New pixels (BGRA, top row first) for the window; drawn at the next paint.</summary>
    public void Present(byte[] bgra, int w, int h)
    {
        lock (gate) { pixels = bgra; frameWidth = w; frameHeight = h; }
        Interlocked.Increment(ref Frames);
        if (hwnd != IntPtr.Zero) InvalidateRect(hwnd, IntPtr.Zero, false);
    }

    // ---- newest frame only: offering is instant, decoding runs on its own thread ------------

    private string? offered;
    private readonly AutoResetEvent offer = new(false);
    private Func<string, (byte[] bgra, int w, int h)?>? decode;

    /// <summary>Hands the window an encoded frame and returns at once. A decoder thread takes the
    /// newest one; frames offered while it decodes replace each other, so the window never falls
    /// behind and nothing waits on it (input included).</summary>
    public void Offer(string frame, Func<string, (byte[] bgra, int w, int h)?> decoder)
    {
        if (decode == null)
        {
            decode = decoder;
            new Thread(Decode) { IsBackground = true, Name = "screen decode: " + title }.Start();
        }
        Interlocked.Exchange(ref offered, frame);
        offer.Set();
    }

    private void Decode()
    {
        while (!Closed)
        {
            offer.WaitOne(500);
            // changed rectangles build on each other: all of them, in order
            while (patches.TryDequeue(out var patch))
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if (patch is byte[] { Length: 9 } echo && echo[0] is 3 or 4)
                    {
                        // 3: the frames before this echo are drawn — now minus the stamp is input → picture
                        // 4: echoed on arrival — now minus the stamp is the pipe's round trip
                        var ms = Environment.TickCount64 - BitConverter.ToInt64(echo, 1);
                        if (echo[0] == 3) picture.Add(ms); else pipe.Add(ms);
                        continue;
                    }
                    var applied = patch is byte[] binary ? ApplyBinary(binary) : Apply((string)patch);
                    if (applied) Interlocked.Increment(ref Frames);
                }
                catch { /* a bad update is skipped */ }
                Stats(System.Diagnostics.Stopwatch.GetTimestamp() - started);
            }

            var frame = Interlocked.Exchange(ref offered, null);
            if (frame == null) continue;
            try { if (decode!(frame) is { } px) Present(px.bgra, px.w, px.h); }
            catch { /* a bad frame is skipped; the next one replaces it */ }
        }
    }

    // ---- changed rectangles: {"rect":[x,y,w,h],"z":"<base64 deflate(BGRA rows)>"} --------------

    private readonly System.Collections.Concurrent.ConcurrentQueue<object> patches = new();   // text lines or binary messages

    /// <summary>
    /// A message from PlangOS's screen, [u8 kind][payload]: 1 a frame's rectangles, 7 a frame that
    /// opens with a move, 3/4 an input's echo (for latency) — these keep their order in the decode
    /// queue — 2 the pointer to show, 5 text PlangOS copied, 9 a message from PlangOS's PLang for
    /// this one (json; it reaches OnInput as the events do). False when it isn't one (an image, say).
    /// </summary>
    public bool Take(byte[] message)
    {
        if (message.Length == 0) return false;
        switch (message[0])
        {
            case 1 or 3 or 4 or 7: PatchBinary(message); return true;
            case 2: Cursor(System.Text.Encoding.UTF8.GetString(message, 1, message.Length - 1)); return true;
            case 5: Clipboard(System.Text.Encoding.UTF8.GetString(message, 1, message.Length - 1)); return true;
            case 9: onEvent((global::app.type.item.text.@this)("{\"guest\":" + System.Text.Encoding.UTF8.GetString(message, 1, message.Length - 1) + "}")); return true;
            default: return false;
        }
    }

    /// <summary>Queues a changed rectangle; applied in order by the decoder thread.</summary>
    public void Patch(string line) => Enqueue(line);

    /// <summary>Queues a binary frame message ([1][u16 count] then per rectangle
    /// [i32 x][i32 y][u32 w][u32 h][u32 n][n bytes QOI]); applied in order like the text ones.</summary>
    public void PatchBinary(byte[] message) => Enqueue(message);

    private void Enqueue(object update)
    {
        if (decode == null)
        {
            decode = _ => null;
            new Thread(Decode) { IsBackground = true, Name = "screen decode: " + title }.Start();
        }
        if (update is byte[] m && IsVideo(m)) Interlocked.Increment(ref videosWaiting);
        patches.Enqueue(update);
        offer.Set();
    }

    // A binary frame — [1] its rectangles, or [7] a move and then its rectangles: all bands decoded
    // on all cores, then the whole frame put in under the gate, so no paint shows half of it (the
    // moved pixels without what the move uncovered); only then is it marked for painting.
    private bool ApplyBinary(byte[] m)
    {
        var at = m[0] == 7 ? 25 : 1;
        var count = BitConverter.ToUInt16(m, at);
        var parts = new (int x, int y, int w, int h, int at, int n)[count];
        at += 2;
        for (var i = 0; i < count; i++)
        {
            int x = BitConverter.ToInt32(m, at), y = BitConverter.ToInt32(m, at + 4);
            int w = (int)BitConverter.ToUInt32(m, at + 8), h = (int)BitConverter.ToUInt32(m, at + 12);
            var n = (int)BitConverter.ToUInt32(m, at + 16);
            parts[i] = (x, y, w, h, at + 20, n);
            at += 20 + n;
        }
        var decoded = new byte[]?[count];
        // a video's pictures in order, one stream at a time; the lossless ones on all cores. When a
        // newer video picture waits behind this one, this one is only decoded (the newer needs it),
        // not shown: behind, the window catches up instead of falling further behind.
        var newest = !IsVideo(m) || Interlocked.Decrement(ref videosWaiting) == 0;
        for (var i = 0; i < count; i++)
            if (parts[i].n > 8 && m.AsSpan(parts[i].at, 4).SequenceEqual("h264"u8))
                decoded[i] = Picture(BitConverter.ToUInt32(m, parts[i].at + 4), m.AsSpan(parts[i].at + 8, parts[i].n - 8), parts[i].w, parts[i].h, newest);
        Parallel.For(0, count, i =>
        {
            if (decoded[i] == null && !m.AsSpan(parts[i].at, 4).SequenceEqual("h264"u8))
                decoded[i] = Qoi(m.AsSpan(parts[i].at, parts[i].n).ToArray(), parts[i].w * parts[i].h);
        });
        Interlocked.Add(ref statBytes, m.Length);
        var areas = new List<RECT>(count + 1);
        lock (gate)
        {
            if (m[0] == 7 && Move(m) is { } moved) areas.Add(moved);
            for (var i = 0; i < count; i++)
                if (decoded[i] is { } px && Copy(parts[i].x, parts[i].y, parts[i].w, parts[i].h, px) is { } area) areas.Add(area);
        }
        // repaint only what changed: WM_PAINT's clip is these areas, so GDI copies just these pixels
        if (hwnd != IntPtr.Zero)
            foreach (var area in areas)
            {
                var a = area;
                InvalidateArea(hwnd, ref a, false);
            }
        return areas.Count > 0;
    }

    // ---- a video playing in PlangOS: an H.264 stream for its part of the screen ----------------

    private Video? video;
    private bool noVideo;
    private int videosWaiting;   // video pictures in the queue, not yet decoded

    /// <summary>A frame message that is a video's picture (PlangOS sends each as a frame of its own).</summary>
    // [1][u16 count] then the first region's [x y w h n] (20 bytes): its bytes start at 23
    private static bool IsVideo(byte[] m) => m.Length > 31 && m[0] == 1 && m.AsSpan(23, 4).SequenceEqual("h264"u8);

    /// <summary>The next picture of video stream <paramref name="id"/> (a new number: a new stream,
    /// decoded anew), or null when it isn't the <paramref name="newest"/>. When Windows can't decode
    /// H.264 (an N edition), PlangOS is told once, and sends its videos losslessly from then on.</summary>
    private byte[]? Picture(uint id, ReadOnlySpan<byte> h264, int w, int h, bool newest)
    {
        if (noVideo) return null;
        try
        {
            if (video?.Id != id)
            {
                video?.Dispose();
                video = null;
                video = new Video(id, w, h);
            }
            return video.Decode(h264, w, h, newest);
        }
        catch (Exception ex)
        {
            noVideo = true;
            video?.Dispose();
            video = null;
            onEvent((global::app.type.item.text.@this)("{\"video\":false,\"why\":" + JsonSerializer.Serialize(ex.Message) + "}"));
            return null;
        }
    }

    // {"rects":[[x,y,w,h,"<qoi>"],…]} (one frame) or {"rect":[x,y,w,h],"qoi":"…"} (one rectangle)
    private bool Apply(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var any = false;
        if (root.TryGetProperty("rects", out var list))
        {
            // a big change arrives as bands: decode them on all cores, then copy them in
            var parts = list.EnumerateArray()
                .Select(e => (x: e[0].GetInt32(), y: e[1].GetInt32(), w: e[2].GetInt32(), h: e[3].GetInt32(), qoi: e[4].GetString()!))
                .ToArray();
            var decoded = new byte[]?[parts.Length];
            Parallel.For(0, parts.Length, i => decoded[i] = Qoi(Convert.FromBase64String(parts[i].qoi), parts[i].w * parts[i].h));
            for (var i = 0; i < parts.Length; i++)
                if (decoded[i] is { } px)
                {
                    Interlocked.Add(ref statBytes, parts[i].qoi.Length);
                    any |= Blit(parts[i].x, parts[i].y, parts[i].w, parts[i].h, px);
                }
        }
        else if (root.TryGetProperty("rect", out var r) && root.TryGetProperty("qoi", out var q))
            any = Apply(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32(), q.GetString()!);
        return any;
    }

    private bool Apply(int x, int y, int w, int h, string qoi)
    {
        var rect = Qoi(Convert.FromBase64String(qoi), w * h);
        if (rect == null) return false;
        Interlocked.Add(ref statBytes, qoi.Length);
        return Blit(x, y, w, h, rect);
    }

    /// <summary>A frame's move (a window dragged): [7][i32 x][i32 y][u32 w][u32 h][i32 dx][i32 dy]…
    /// The pixels are here already: they move here, and aren't sent again. Under the gate; the
    /// area they landed on, or null.</summary>
    private RECT? Move(byte[] m)
    {
        int x = BitConverter.ToInt32(m, 1), y = BitConverter.ToInt32(m, 5);
        int w = BitConverter.ToInt32(m, 9), h = BitConverter.ToInt32(m, 13);
        int dx = BitConverter.ToInt32(m, 17), dy = BitConverter.ToInt32(m, 21);
        // the source, cut to what is on the screen both where it is and where it lands
        // (PlangOS sends it cut already; this only guards the copy)
        var left = Math.Max(x, Math.Max(0, -dx));
        var top = Math.Max(y, Math.Max(0, -dy));
        var right = Math.Min(x + w, Math.Min(frameWidth, frameWidth - dx));
        var bottom = Math.Min(y + h, Math.Min(frameHeight, frameHeight - dy));
        if (right <= left || bottom <= top || pixels.Length != frameWidth * frameHeight * 4) return null;
        var bytes = (right - left) * 4;
        // rows in the order that doesn't overwrite what is still to be moved; within a row BlockCopy is overlap-safe
        for (var i = 0; i < bottom - top; i++)
        {
            var row = dy > 0 ? bottom - 1 - i : top + i;
            Buffer.BlockCopy(pixels, (row * frameWidth + left) * 4, pixels, ((row + dy) * frameWidth + left + dx) * 4, bytes);
        }
        return new RECT { left = left + dx, top = top + dy, right = right + dx, bottom = bottom + dy };
    }

    /// <summary>One rectangle of a text frame, put in and marked for painting.</summary>
    private bool Blit(int x, int y, int w, int h, byte[] rect)
    {
        RECT? area;
        lock (gate) area = Copy(x, y, w, h, rect);
        if (area is not { } a) return false;
        if (hwnd != IntPtr.Zero) InvalidateArea(hwnd, ref a, false);
        return true;
    }

    /// <summary>A rectangle's pixels into the frame. Under the gate; the area, or null.</summary>
    private RECT? Copy(int x, int y, int w, int h, byte[] rect)
    {
        if (frameWidth != width || frameHeight != height || pixels.Length != width * height * 4)
        {
            pixels = new byte[width * height * 4];
            frameWidth = width; frameHeight = height;
        }
        var rows = Math.Min(h, height - y);
        var cols = Math.Min(w, width - x);
        if (rows <= 0 || cols <= 0 || x < 0 || y < 0) return null;
        for (var row = 0; row < rows; row++)
            Buffer.BlockCopy(rect, row * w * 4, pixels, ((y + row) * width + x) * 4, cols * 4);
        return new RECT { left = x, top = y, right = x + w, bottom = y + h };
    }

    // ---- live numbers in the title: updates/s, MB/s, time to apply ------------------------------

    private long statBytes, statUpdates, statTicks, statSince = Environment.TickCount64;
    private readonly Samples pipe = new(), picture = new();   // the pipe's round trip; input → picture
    private readonly HashSet<(int sc, bool ext)> heldKeys = new();   // keys sent as down and not yet up (window thread only)

    /// <summary>The last 64 latencies of one kind; they say their own percentiles.</summary>
    private sealed class Samples
    {
        private readonly long[] values = new long[64];
        private int count;

        public void Add(long ms)
        {
            if (ms < 0 || ms > 10_000) return;
            lock (values) values[count++ % values.Length] = ms;
        }

        /// <summary>The <paramref name="percent"/>th percentile, in ms (-1 before any sample).</summary>
        public long At(int percent)
        {
            lock (values)
            {
                var n = Math.Min(count, values.Length);
                if (n == 0) return -1;
                var sorted = values.Take(n).OrderBy(v => v).ToArray();
                return sorted[Math.Min(n - 1, n * percent / 100)];
            }
        }

        public string Json() => $"{{\"p50\":{At(50)},\"p85\":{At(85)},\"p95\":{At(95)}}}";
    }

    /// <summary>Once a second the numbers go to the screen's other side (PlangOS shows them on its
    /// taskbar): updates/s, MB/s, ms to apply, input → picture and the pipe's round trip.</summary>
    private void Stats(long appliedTicks)
    {
        statUpdates++;
        statTicks += appliedTicks;
        var now = Environment.TickCount64;
        if (now - statSince < 1000 || hwnd == IntPtr.Zero) return;
        var seconds = (now - statSince) / 1000.0;
        var ms = statUpdates == 0 ? 0 : statTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / statUpdates;
        var mb = Interlocked.Exchange(ref statBytes, 0) / 1048576.0 / seconds;
        var numbers = System.Globalization.CultureInfo.InvariantCulture;
        var stats = "{\"stats\":{" + string.Format(numbers, "\"updates\":{0:F0},\"mb\":{1:F2},\"apply\":{2:F1},\"queued\":{3}", statUpdates / seconds, mb, ms, patches.Count)
            + ",\"picture\":" + picture.Json() + ",\"pipe\":" + pipe.Json() + "}}";
        onEvent((global::app.type.item.text.@this)stats);
        statUpdates = 0; statTicks = 0; statSince = now;
    }

    /// <summary>QOI decoder (qoiformat.org): 4 channels out, in the order they went in (BGRA here).</summary>
    private static byte[]? Qoi(byte[] d, int pixelCount)
    {
        if (d.Length < 22 || d[0] != (byte)'q' || d[1] != (byte)'o' || d[2] != (byte)'i' || d[3] != (byte)'f') return null;
        var outp = new byte[pixelCount * 4];
        var index = new byte[64 * 4];
        byte c0 = 0, c1 = 0, c2 = 0, c3 = 255;
        int p = 14, run = 0;
        var end = d.Length - 8;
        for (var o = 0; o < outp.Length; o += 4)
        {
            if (run > 0) run--;
            else if (p < end)
            {
                int b = d[p++];
                if (b == 0xFE) { c0 = d[p++]; c1 = d[p++]; c2 = d[p++]; }
                else if (b == 0xFF) { c0 = d[p++]; c1 = d[p++]; c2 = d[p++]; c3 = d[p++]; }
                else switch (b >> 6)
                {
                    case 0: var i = (b & 63) * 4; c0 = index[i]; c1 = index[i + 1]; c2 = index[i + 2]; c3 = index[i + 3]; break;
                    case 1:
                        c0 = unchecked((byte)(c0 + ((b >> 4) & 3) - 2));
                        c1 = unchecked((byte)(c1 + ((b >> 2) & 3) - 2));
                        c2 = unchecked((byte)(c2 + (b & 3) - 2));
                        break;
                    case 2:
                        int b2 = d[p++]; var vg = (b & 63) - 32;
                        c0 = unchecked((byte)(c0 + vg - 8 + ((b2 >> 4) & 15)));
                        c1 = unchecked((byte)(c1 + vg));
                        c2 = unchecked((byte)(c2 + vg - 8 + (b2 & 15)));
                        break;
                    case 3: run = b & 63; break;
                }
                var h = ((c0 * 3 + c1 * 5 + c2 * 7 + c3 * 11) % 64) * 4;
                index[h] = c0; index[h + 1] = c1; index[h + 2] = c2; index[h + 3] = c3;
            }
            outp[o] = c0; outp[o + 1] = c1; outp[o + 2] = c2; outp[o + 3] = c3;
        }
        return outp;
    }

    // ---- cursor: the page says what it is (CSS names) ------------------------------------------

    private IntPtr cursor;

    /// <summary>Shows the pointer the page asks for: pointer (hand), text (I-beam), wait, … </summary>
    public void Cursor(string css)
    {
        var id = css switch
        {
            "pointer" => IDC_HAND, "text" or "vertical-text" => IDC_IBEAM, "wait" => IDC_WAIT,
            "progress" => IDC_APPSTARTING, "crosshair" => IDC_CROSS, "move" or "all-scroll" => IDC_SIZEALL,
            "not-allowed" or "no-drop" => IDC_NO, "help" => IDC_HELP,
            "ew-resize" or "col-resize" or "e-resize" or "w-resize" => IDC_SIZEWE,
            "ns-resize" or "row-resize" or "n-resize" or "s-resize" => IDC_SIZENS,
            "nwse-resize" or "nw-resize" or "se-resize" => IDC_SIZENWSE,
            "nesw-resize" or "ne-resize" or "sw-resize" => IDC_SIZENESW,
            _ => IDC_ARROW,
        };
        cursor = LoadCursorW(IntPtr.Zero, id);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, WM_APP_CURSOR, IntPtr.Zero, IntPtr.Zero);
    }

    // ---- clipboard: text, both ways ------------------------------------------------------------

    private string? copied;   // what PlangOS copied, waiting for the window's thread to put it on Windows' clipboard
    private string? shared;   // the last text sent either way: the same text isn't sent back

    /// <summary>PlangOS copied text: it goes on Windows' clipboard (on the window's thread, which owns it).</summary>
    public void Clipboard(string text)
    {
        copied = text;
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, WM_APP_CLIPBOARD, IntPtr.Zero, IntPtr.Zero);
    }

    private void PutClipboard()
    {
        if (Interlocked.Exchange(ref copied, null) is not { } text || !OpenClipboardRetry()) return;
        try
        {
            EmptyClipboard();
            var bytes = (text.Length + 1) * 2;
            var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes);
            if (memory == IntPtr.Zero) return;
            var at = GlobalLock(memory);
            Marshal.Copy((text + "\0").ToCharArray(), 0, at, text.Length + 1);
            GlobalUnlock(memory);
            if (SetClipboardData(CF_UNICODETEXT, memory) != IntPtr.Zero) shared = text;   // the clipboard owns memory now
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Windows' clipboard text to PlangOS, when it's new and not ours.</summary>
    private void ShareClipboard()
    {
        if (GetClipboardOwner() == hwnd || !OpenClipboardRetry()) return;
        string? text = null;
        try
        {
            var memory = GetClipboardData(CF_UNICODETEXT);
            if (memory != IntPtr.Zero)
            {
                var at = GlobalLock(memory);
                if (at != IntPtr.Zero) { text = Marshal.PtrToStringUni(at); GlobalUnlock(memory); }
            }
        }
        finally { CloseClipboard(); }
        if (text == null || text == shared) return;
        shared = text;
        onEvent(new global::app.type.item.clipboard.@this((global::app.type.item.text.@this)text));
    }

    // another program may hold the clipboard for a moment
    private bool OpenClipboardRetry()
    {
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(hwnd)) return true;
            Thread.Sleep(5);
        }
        return false;
    }

    public void Close()
    {
        if (hwnd != IntPtr.Zero && !Closed) PostMessageW(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void Run()
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));   // per-monitor v2: one drawn pixel = one screen pixel
            var instance = GetModuleHandleW(null);
            var className = "PlangOS.Screen." + Interlocked.Increment(ref windows);
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = CS_DBLCLKS | CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(proc),
                hInstance = instance,
                hCursor = LoadCursorW(IntPtr.Zero, IDC_ARROW),
                lpszClassName = className,
            };
            if (RegisterClassExW(ref wc) == 0) throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());

            // borderless: the window is only the picture — no title bar, frame or buttons.
            // Alt+F4 still closes it, Alt+Tab and the Windows key still switch away.
            const uint style = WS_POPUP | WS_MINIMIZEBOX;
            var rect = new RECT { right = width, bottom = height };
            AdjustWindowRectEx(ref rect, style, false, 0);
            int w = rect.right - rect.left, h = rect.bottom - rect.top;
            int x = Math.Max(0, (GetSystemMetrics(SM_CXSCREEN) - w) / 2), y = Math.Max(0, (GetSystemMetrics(SM_CYSCREEN) - h) / 2);
            hwnd = CreateWindowExW(0, className, title, style | WS_VISIBLE, x, y, w, h, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
            SetForegroundWindow(hwnd);
            AddClipboardFormatListener(hwnd);   // WM_CLIPBOARDUPDATE when anything on Windows copies
        }
        catch (Exception ex) { error = ex.Message; Closed = true; ready.Set(); return; }
        ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        Closed = true;
        onClosed();
    }

    // Windows packs two signed 16-bit values into one parameter (x/y, wheel delta). PLang builds with
    // overflow checks on, so the narrowing must be unchecked: a negative value arrives as 0xFFxx.
    private static short Low(IntPtr value) => unchecked((short)(value.ToInt64() & 0xFFFF));
    private static short High(IntPtr value) => unchecked((short)((value.ToInt64() >> 16) & 0xFFFF));

    // The OS calls this; an exception escaping it would end the whole process. Never let one out.
    private IntPtr Proc(IntPtr h, uint message, IntPtr wParam, IntPtr lParam)
    {
        try { return Handle(h, message, wParam, lParam); }
        catch { return DefWindowProcW(h, message, wParam, lParam); }
    }

    private IntPtr Handle(IntPtr h, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WM_PAINT: Paint(h); return IntPtr.Zero;
            case WM_SETCURSOR:
                if ((lParam.ToInt64() & 0xFFFF) != HTCLIENT) break;     // the frame and title bar keep theirs
                SetCursor(cursor != IntPtr.Zero ? cursor : LoadCursorW(IntPtr.Zero, IDC_ARROW));
                return new IntPtr(1);
            case WM_APP_CURSOR:
                SetCursor(cursor);
                return IntPtr.Zero;
            case WM_APP_CLIPBOARD: PutClipboard(); return IntPtr.Zero;
            case WM_CLIPBOARDUPDATE: ShareClipboard(); return IntPtr.Zero;
            case WM_ERASEBKGND: return new IntPtr(1);    // the frame covers everything: no flicker
            case WM_CLOSE: DestroyWindow(h); return IntPtr.Zero;
            // Alt or F10 alone would put this window in Windows' menu mode, which eats the next keys;
            // they belong to PlangOS. (Alt+F4 is SC_CLOSE, not this: it still closes the window.)
            case WM_SYSCOMMAND when (wParam.ToInt64() & 0xFFF0) == SC_KEYMENU: return IntPtr.Zero;
            // focus leaving (Alt+Tab …): every key still held here would stay held in PlangOS —
            // a stuck Alt turns clicks into downloads and keys into menu shortcuts. Release them.
            case WM_KILLFOCUS:
                foreach (var (sc, ext) in heldKeys.ToArray())
                    onEvent(new global::app.type.item.input.key.@this(false, (uint)sc, ext));
                heldKeys.Clear();
                break;
            case WM_DESTROY: RemoveClipboardFormatListener(h); PostQuitMessage(0); return IntPtr.Zero;

            case WM_MOUSEMOVE:
                var held = Held(wParam);
                var now = Environment.TickCount64;
                if (held == "none" && now - lastMove < 16) return IntPtr.Zero;   // moves are many: one per frame is enough
                lastMove = now;
                Mouse("move", lParam, held, 0);
                return IntPtr.Zero;
            case WM_LBUTTONDOWN: SetCapture(h); Mouse("down", lParam, "left", 1); return IntPtr.Zero;
            case WM_LBUTTONDBLCLK: Mouse("down", lParam, "left", 2); return IntPtr.Zero;
            case WM_LBUTTONUP: ReleaseCapture(); Mouse("up", lParam, "left", 1); return IntPtr.Zero;
            case WM_RBUTTONDOWN: Mouse("down", lParam, "right", 1); return IntPtr.Zero;
            case WM_RBUTTONUP: Mouse("up", lParam, "right", 1); return IntPtr.Zero;
            case WM_MBUTTONDOWN: Mouse("down", lParam, "middle", 1); return IntPtr.Zero;
            case WM_MBUTTONUP: Mouse("up", lParam, "middle", 1); return IntPtr.Zero;
            // the window class asks for double-clicks, so the second press of any button comes as its own message
            case WM_RBUTTONDBLCLK: Mouse("down", lParam, "right", 2); return IntPtr.Zero;
            case WM_MBUTTONDBLCLK: Mouse("down", lParam, "middle", 2); return IntPtr.Zero;
            // the mouse's side buttons: XBUTTON1 is back, XBUTTON2 is forward (Windows wants TRUE back)
            case WM_XBUTTONDOWN: case WM_XBUTTONDBLCLK: case WM_XBUTTONUP:
                var side = High(wParam) == 1 ? "back" : "forward";
                Mouse(message == WM_XBUTTONUP ? "up" : "down", lParam, side, 1);
                return new IntPtr(1);
            case WM_MOUSEWHEEL:
                var point = new POINT { x = Low(lParam), y = High(lParam) };
                ScreenToClient(h, ref point);   // wheel positions are screen coordinates
                var delta = High(wParam);          // negative when scrolling down
                onEvent(new global::app.type.item.input.mouse.@this(global::app.type.item.input.mouse.Gesture.wheel, point.x, point.y,
                    dy: -delta, mods: Mods(), stamp: Environment.TickCount64));
                return IntPtr.Zero;

            case WM_KEYDOWN: case WM_SYSKEYDOWN:
                if (Key("down", unchecked((int)wParam.ToInt64()), lParam)) return IntPtr.Zero;
                break;   // Alt+F4 and friends stay the system's
            case WM_KEYUP: case WM_SYSKEYUP:
                if (Key("up", unchecked((int)wParam.ToInt64()), lParam)) return IntPtr.Zero;
                break;
            case WM_CHAR:
                var c = unchecked((char)wParam.ToInt64());
                if (c >= ' ') onEvent(new global::app.type.item.input.text.@this(c.ToString()));
                return IntPtr.Zero;
        }
        return DefWindowProcW(h, message, wParam, lParam);
    }

    private void Paint(IntPtr h)
    {
        BeginPaint(h, out var ps);
        lock (gate)
        {
            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = frameWidth, biHeight = -frameHeight,   // negative: top row first
                    biPlanes = 1, biBitCount = 32, biCompression = 0,
                },
            };
            SetDIBitsToDevice(ps.hdc, 0, 0, (uint)frameWidth, (uint)frameHeight, 0, 0, 0, (uint)frameHeight, pixels, ref info, 0);
        }
        EndPaint(h, ref ps);
    }

    private void Mouse(string kind, IntPtr lParam, string button, int clicks)
    {
        int x = Low(lParam), y = High(lParam);   // negative outside the window while dragging
        // a click is stamped: the screen echoes it after the next frame, which times input → picture
        onEvent(new global::app.type.item.input.mouse.@this(Enum.Parse<global::app.type.item.input.mouse.Gesture>(kind), x, y,
            Enum.Parse<global::app.type.item.input.mouse.Button>(button), clicks, mods: Mods(),
            stamp: kind == "move" ? null : Environment.TickCount64));
    }

    // Every key goes out with its scancode (sc, ext): a real keyboard for a compositor, which applies
    // its own layout. Keys that aren't text also carry a name for DevTools; browser shortcuts also go
    // as navigation. Typed characters come through WM_CHAR as text too — each side takes what it uses.
    private bool Key(string kind, int vk, IntPtr lParam)
    {
        var mods = Mods();
        var alt = (mods & 1) != 0; var ctrl = (mods & 2) != 0;
        var sc = (int)((lParam.ToInt64() >> 16) & 0xFF);
        var ext = ((lParam.ToInt64() >> 24) & 1) != 0;
        if (kind == "down" && alt && vk == VK_LEFT) onEvent(new global::app.type.item.input.navigate.@this(global::app.type.item.input.navigate.Direction.back));
        if (kind == "down" && alt && vk == VK_RIGHT) onEvent(new global::app.type.item.input.navigate.@this(global::app.type.item.input.navigate.Direction.forward));
        if (kind == "down" && vk == VK_F5) onEvent(new global::app.type.item.input.navigate.@this(global::app.type.item.input.navigate.Direction.reload));
        // paste: Windows' clipboard reaches PlangOS before the keys do (copied before this window
        // listened, or before PlangOS was up)
        if (kind == "down" && ((ctrl && vk == 'V') || ((mods & 8) != 0 && vk == VK_INSERT))) ShareClipboard();
        if (kind == "down") heldKeys.Add((sc, ext)); else heldKeys.Remove((sc, ext));
        // the key names itself from its virtual key (the key type owns its codes)
        onEvent(new global::app.type.item.input.key.@this(kind == "down", (uint)sc, ext, vk, mods: mods,
            stamp: kind == "down" ? Environment.TickCount64 : null));
        return !alt;   // Alt combinations stay Windows' too (Alt+F4 closes the window)
    }

    private static string Held(IntPtr wParam)
    {
        var w = wParam.ToInt64();
        return (w & MK_LBUTTON) != 0 ? "left" : (w & MK_RBUTTON) != 0 ? "right" : (w & MK_MBUTTON) != 0 ? "middle" : "none";
    }

    // DevTools modifier bits: Alt 1, Ctrl 2, Meta 4, Shift 8
    private static int Mods()
    {
        int bits = 0;
        if (GetKeyState(VK_MENU) < 0) bits |= 1;
        if (GetKeyState(VK_CONTROL) < 0) bits |= 2;
        if (GetKeyState(VK_SHIFT) < 0) bits |= 8;
        return bits;
    }

    // ---- Win32 -------------------------------------------------------------------------------

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint CS_VREDRAW = 0x1, CS_HREDRAW = 0x2, CS_DBLCLKS = 0x8;
    private const uint WS_POPUP = 0x80000000, WS_MINIMIZEBOX = 0x00020000, WS_VISIBLE = 0x10000000;
    private const uint WM_DESTROY = 0x2, WM_PAINT = 0xF, WM_CLOSE = 0x10, WM_ERASEBKGND = 0x14;
    private const uint WM_KILLFOCUS = 0x8, WM_SYSCOMMAND = 0x112;
    private const long SC_KEYMENU = 0xF100;
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_CHAR = 0x102, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    private const uint WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_LBUTTONDBLCLK = 0x203;
    private const uint WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208, WM_MOUSEWHEEL = 0x20A;
    private const uint WM_RBUTTONDBLCLK = 0x206, WM_MBUTTONDBLCLK = 0x209, WM_XBUTTONDOWN = 0x20B, WM_XBUTTONUP = 0x20C, WM_XBUTTONDBLCLK = 0x20D;
    private const int MK_LBUTTON = 0x1, MK_RBUTTON = 0x2, MK_MBUTTON = 0x10;
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_F5 = 0x74;
    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;
    private const int IDC_SIZENWSE = 32642, IDC_SIZENESW = 32643;
    private const int IDC_ARROW = 32512, IDC_IBEAM = 32513, IDC_WAIT = 32514, IDC_CROSS = 32515, IDC_SIZEWE = 32644,
        IDC_SIZENS = 32645, IDC_SIZEALL = 32646, IDC_NO = 32648, IDC_HAND = 32649, IDC_APPSTARTING = 32650, IDC_HELP = 32651;
    private const uint WM_SETCURSOR = 0x20, WM_APP_CURSOR = 0x8001;
    private const long HTCLIENT = 1;
    private const uint WM_CLIPBOARDUPDATE = 0x31D, WM_APP_CLIPBOARD = 0x8002, CF_UNICODETEXT = 13, GMEM_MOVEABLE = 0x2;
    private const int VK_INSERT = 0x2D;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style; public IntPtr lpfnWndProc; public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName, lpszClassName; public IntPtr hIconSm;
    }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc; public int fErase; public RECT rcPaint; public int fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
    [DllImport("user32.dll", EntryPoint = "InvalidateRect")] private static extern bool InvalidateArea(IntPtr hWnd, ref RECT rect, bool erase);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(IntPtr hWnd, string text);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, int name);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] private static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("gdi32.dll")]
    private static extern int SetDIBitsToDevice(IntPtr hdc, int x, int y, uint w, uint h, int srcX, int srcY, uint startScan, uint lines, byte[] bits, ref BITMAPINFO info, uint usage);
}
