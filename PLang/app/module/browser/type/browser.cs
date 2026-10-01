using app.Attributes;

namespace app.module.browser;

/// <summary>
/// A browser started with <c>browser.start</c>. On a screen (PlangOS's display) its pages are
/// windows — the first one the desktop (<c>%browser.desktop%</c>), more with <c>window.open</c>.
/// Without one it renders a page off-screen and delivers each new frame through OnFrame as
/// <c>%!data%</c>. <c>browser.send</c> gives it mouse, keyboard and navigation; <c>browser.stop</c> ends it.
/// </summary>
[PlangType("browser")]
public sealed class Browser : global::app.type.item.@this, global::app.type.item.ICreate<Browser>
{
    private global::app.module.window.Windows? windows;

    /// <summary>The desktop: the first page, the whole screen (on a screen).</summary>
    [LlmBuilder, Out] public global::app.module.window.Window Desktop => Windows.Desktop;

    /// <summary>Its windows on the screen.</summary>
    internal global::app.module.window.Windows Windows => windows ??= new(this);

    /// <summary>The page it opened first.</summary>
    [LlmBuilder, Out] public string Url { get; set; } = "";

    /// <summary>Frame width in pixels.</summary>
    [LlmBuilder, Out] public int Width { get; set; }

    /// <summary>Frame height in pixels.</summary>
    [LlmBuilder, Out] public int Height { get; set; }

    /// <summary>True until the browser exits.</summary>
    [LlmBuilder, Out] public bool Running => Os is { HasExited: false };

    internal System.Diagnostics.Process? Os { get; set; }
    /// <summary>The Chromium it runs, to open more windows in it.</summary>
    internal global::app.type.item.path.file.@this? Program { get; set; }
    /// <summary>DevTools' port, on 127.0.0.1: the pages of its windows.</summary>
    internal int Port { get; set; }
    /// <summary>DevTools for the whole browser (its pages and windows), beside Page (the first page).</summary>
    internal System.Net.WebSockets.ClientWebSocket? Control { get; set; }
    /// <summary>The desktop's browser window: a page that lands in it was opened as a tab.</summary>
    internal int DesktopWindow { get; set; }
    /// <summary>The context it was started in: what goes wrong away from any step (a window that can't be
    /// paired with its page) is written to its app's error channel.</summary>
    internal actor.context.@this? Context { get; set; }
    /// <summary>Hands what a page of the app's own says (<c>plang(text)</c>) to OnMessage.</summary>
    internal Func<string, Task>? Message { get; set; }
    /// <summary>Where plang's own pages are (<c>file://</c> under these folders): the app's folder, and the
    /// runtime's os folder (the system's pages — only the system writes there). They may talk with plang;
    /// no other page.</summary>
    internal string[] Roots { get; set; } = [];
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
    internal global::app.module.screen.code.wayland.Address AddressOf(string address) => new(Shown(address), address);
    /// <summary>The screen Chromium draws onto (PlangOS's display), from <c>screen.open</c>.</summary>
    internal global::app.module.screen.Screen? Screen { get; set; }
    /// <summary>The page, off-screen (no screen): its frames and input go over this.</summary>
    internal System.Net.WebSockets.ClientWebSocket? Page { get; set; }
    internal SemaphoreSlim Sending { get; } = new(1, 1);
    internal int MessageId;
    /// <summary>DevTools requests waiting for their reply, by id.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<string>> Pending { get; } = new();
    /// <summary>Sends a line up alongside the frames (the pointer the page wants).</summary>
    internal Func<string, Task>? Emit { get; set; }
    internal string LastCursor = "default";
    internal long LastCursorAsk;
    internal int CursorBusy;

    public override string ToString() => $"browser {Url} ({Width}x{Height}{(Running ? "" : ", exited")})";
}
