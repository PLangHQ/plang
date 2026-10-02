using app.module.screen.code;

namespace app.module.screen;

/// <summary>
/// Opens a window to draw frames in, centred on the main monitor, and returns at once. Each mouse
/// or keyboard event in it calls OnInput with <c>%!data%</c> = one JSON line
/// (<c>{"mouse":…}</c>, <c>{"key":…}</c>, <c>{"text":…}</c>, <c>{"nav":…}</c> — what browser.send takes).
/// Closing the window calls OnClose.
/// </summary>
[Action("open", Cacheable = false)]
public partial class open : IContext
{
    /// <summary>The window's title. Default "PlangOS".</summary>
    [Default("PlangOS")]
    public partial data.@this<global::app.type.item.text.@this> Title { get; init; }

    /// <summary>Width of the drawing area in pixels. Default 1920.</summary>
    [Default(1920)]
    public partial data.@this<global::app.type.item.number.@this> Width { get; init; }

    /// <summary>Height of the drawing area in pixels. Default 1080.</summary>
    [Default(1080)]
    public partial data.@this<global::app.type.item.number.@this> Height { get; init; }

    /// <summary>Goal called for each mouse or keyboard event, the event as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnInput { get; init; }

    /// <summary>Goal called when the window is closed.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnClose { get; init; }

    /// <summary>PlangOS (Linux): the screen's frames go to this app's own output — the pipe to the
    /// plang that shows them — as binary messages.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> ToOutput { get; init; }

    /// <summary>PlangOS (Linux): goal called when a window on the screen opens, gets a title, is
    /// focused, minimized, maximized, restored or closed — and when its address field or menu asks to
    /// go somewhere. <c>%!data%</c> is a dict: <c>{"window":"opened","id":1,"title":…}</c>,
    /// <c>{"navigate":…,"id":…}</c>, <c>{"open":…,"id":…}</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnWindow { get; init; }

    [Code]
    public partial IScreen Provider { get; }

    public async Task<data.@this<type.screen.@this>> Start() => await Provider.Open(this);
}
