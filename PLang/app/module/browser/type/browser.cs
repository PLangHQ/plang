using app.Attributes;

namespace app.module.browser;

/// <summary>
/// A browser started with <c>browser.start</c>: it renders a page off-screen and delivers each new
/// frame through OnFrame as <c>%!data%</c>; <c>browser.send</c> gives it mouse, keyboard and
/// navigation; <c>browser.stop</c> ends it.
/// </summary>
[PlangType("browser")]
public sealed class Browser : global::app.type.item.@this, global::app.type.item.ICreate<Browser>
{
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
    /// <summary>Which page each window shows.</summary>
    internal Pages Pages { get; } = new();
    /// <summary>DevTools for the whole browser (its pages and windows), beside Page (the first page).</summary>
    internal System.Net.WebSockets.ClientWebSocket? Control { get; set; }
    /// <summary>The desktop's browser window: a page that lands in it was opened as a tab.</summary>
    internal int DesktopWindow { get; set; }
    /// <summary>Hands what the first page says (<c>plang(text)</c>) to OnMessage.</summary>
    internal Func<string, Task>? Message { get; set; }
    /// <summary>The app's own pages (<c>file://</c> under this folder) may talk with plang; no other page.</summary>
    internal string Root { get; set; } = "";
    /// <summary>The windows showing one of the app's own pages, by window id: plang talks with them.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<long, Talk> Talks { get; } = new();
    /// <summary>The screen Chromium draws onto (PlangOS's display), from <c>screen.open</c>.</summary>
    internal global::app.module.screen.Screen? Screen { get; set; }
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
