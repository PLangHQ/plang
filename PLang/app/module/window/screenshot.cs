using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>
/// How a window's page looks now: a PNG, as base64 text — what an agent looks at to see what it changed.
/// </summary>
[Action("screenshot", Cacheable = false)]
public partial class screenshot : IContext
{
    /// <summary>The window: one from <c>window.open</c>, <c>%browser.desktop%</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await type.window.@this.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        try
        {
            var png = await window.Screenshot();
            return png.Length == 0
                ? Context.Error(new global::app.error.ActionError("The page gave no picture.", "NoScreenshot", 500))
                : Context.Ok<global::app.type.item.text.@this>(png);
        }
        catch (Exception ex) when (ex is TimeoutException or System.Net.WebSockets.WebSocketException)
        {
            return Context.Error(new global::app.error.ActionError($"The page didn't give its picture: {ex.Message}", "ScreenshotTimeout", 504));
        }
    }
}
