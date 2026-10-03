using Browser = app.module.browser.type.browser.@this;
using OnScreen = app.module.browser.type.browser.screen.@this;

namespace app.module.window;

/// <summary>
/// Opens a page in a window of its own, in a running browser on the screen (from
/// <c>browser.start</c>): an app window, with PlangOS's title bar and no tabs. Returns the window once
/// Chromium has opened it; it is shown when its page is, and what it is asked to do (navigate, post,
/// call) waits for that. A <c>file://</c> page is read as the one who opens it.
/// </summary>
[Action("open", Cacheable = false)]
public partial class open : IContext
{
    /// <summary>The page to open.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    public async Task<data.@this<type.window.@this>> Start()
    {
        var browser = await Browser.Value();
        if (browser is not OnScreen onScreen || !onScreen.Running.Value)
            return data.@this<type.window.@this>.From(Context.Error(new global::app.error.ActionError($"The browser can't open windows: {browser}", "BrowserNotRunning", 409)));
        if (await Url.Value() is not global::app.type.item.text.@this url)
            return data.@this<type.window.@this>.From(Context.Error(new global::app.error.ActionError($"No page to open: Url is {Url.Peek()}", "UrlMissing", 400)));
        // waiting before it opens: its title may come first, and pairs it
        var window = onScreen.window.Opening();
        var opened = await onScreen.Open(url.Clr<string>() ?? "", Context);
        if (opened.Success) return Context.Ok<type.window.@this>(window);
        onScreen.window.Unopened(window);
        return data.@this<type.window.@this>.From(opened);
    }
}
