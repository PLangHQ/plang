using app.Attributes;

namespace app.module.screen;

/// <summary>
/// A window opened with <c>screen.open</c>: <c>screen.draw</c> puts a frame in it; its mouse and
/// keyboard come out through OnInput as <c>%!data%</c>, one JSON line per event; closing it calls
/// OnClose. Windows only for now.
/// </summary>
[PlangType("screen")]
public sealed class Screen : global::app.type.item.@this, global::app.type.item.ICreate<Screen>
{
    /// <summary>The window's title.</summary>
    [LlmBuilder, Out] public string Title { get; set; } = "";

    /// <summary>Width of the drawing area in pixels.</summary>
    [LlmBuilder, Out] public int Width { get; set; }

    /// <summary>Height of the drawing area in pixels.</summary>
    [LlmBuilder, Out] public int Height { get; set; }

    /// <summary>True until the window is closed.</summary>
    [LlmBuilder, Out] public bool Open => Window is { Closed: false } || Display != null;

    /// <summary>Frames drawn so far.</summary>
    [LlmBuilder, Out] public int Frames => Window?.Frames ?? 0;

    /// <summary>The Win32 window that shows frames (Windows).</summary>
    internal code.Window? Window { get; set; }

    /// <summary>A message from PlangOS's screen, straight to the window (no goal per frame):
    /// what <c>terminal.open … binary output to %screen%</c> does with each one.</summary>
    internal bool Show(byte[] message) => Window is { Closed: false } window && window.Take(message);
    /// <summary>PlangOS's display that makes them (Linux): programs draw onto it.</summary>
    internal code.wayland.Display? Display { get; set; }
    /// <summary>Where programs find the display: its socket's folder and name.</summary>
    internal string Runtime { get; set; } = "";
    internal string Socket { get; set; } = "";

    public override string ToString() => $"screen '{Title}' {Width}x{Height}{(Open ? "" : ", closed")}";
}
