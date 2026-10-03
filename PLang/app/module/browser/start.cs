using app.Attributes;
using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Starts a browser that renders a page off-screen (no window) and returns at once. Each new frame
/// calls OnFrame with <c>%!data%</c> = one line of JSON: <c>{"frame":"&lt;base64&gt;","format":"jpeg","w":…,"h":…}</c>.
/// Only the newest frame is delivered: a slow OnFrame skips frames instead of falling behind.
/// </summary>
[Action("start", Cacheable = false)]
[RequiresCapability("browser")]
public partial class start : IContext
{
    /// <summary>The page to open.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>Frame width in pixels. Default 1920.</summary>
    [Default(1920)]
    public partial data.@this<global::app.type.item.number.@this> Width { get; init; }

    /// <summary>Frame height in pixels. Default 1080.</summary>
    [Default(1080)]
    public partial data.@this<global::app.type.item.number.@this> Height { get; init; }

    /// <summary>Frame image format: jpeg (small) or png (exact). Default jpeg.</summary>
    [Default("jpeg")]
    public partial data.@this<global::app.type.item.text.@this> Format { get; init; }

    /// <summary>JPEG quality, 1–100. Default 80.</summary>
    [Default(80)]
    public partial data.@this<global::app.type.item.number.@this> Quality { get; init; }

    /// <summary>Goal called with each new frame as <c>%!data%</c>.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnFrame { get; init; }

    /// <summary>Goal called when the first page calls <c>plang(text)</c>. <c>%!data%</c> is the text,
    /// or a dict when the text is a json object.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? OnMessage { get; init; }

    /// <summary>The screen to draw onto (PlangOS's display, from <c>screen.open</c>): Chromium runs as
    /// a normal browser there, each page in a window of its own; the screen makes the frames and
    /// gives it the pointer and keyboard. Without one, Chromium runs headless and frames come
    /// through OnFrame.</summary>
    public partial data.@this<global::app.module.screen.type.screen.@this>? Screen { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this<type.browser.@this>> Start() => await Provider.Start(this);
}
