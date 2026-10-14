using Browser = app.module.browser.type.browser.@this;
using OnScreen = app.module.browser.type.browser.screen.@this;

namespace app.module.window;

/// <summary>
/// Opens a page in a window of its own, in a running browser on the screen (from
/// <c>browser.start</c>): an app window, with PlangOS's title bar and no tabs. Returns the window once
/// Chromium has opened it; it is shown when its page is, and what it is asked to do (navigate, post,
/// call) waits for that. Its page is a path: plang's (<c>/app/writer/start.html</c>; <c>/system/…</c> the system's), read
/// as the one who opens it, or a website.
/// </summary>
[Action("open", Cacheable = false)]
public partial class open : IContext
{
    /// <summary>The page to open: a plang path (<c>/app/writer/start.html</c>; read as the opener), or a website.</summary>
    public partial data.@this<global::app.module.browser.type.page.@this> Url { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    public async Task<data.@this<type.window.@this>> Start()
    {
        var browser = await Browser.Value();
        if (browser is not OnScreen onScreen || !onScreen.Running.Value)
            return data.@this<type.window.@this>.From(Context.Error(new global::app.error.ActionError($"The browser can't open windows: {browser}", "BrowserNotRunning", 409)));
        if (await Url.Value() is not global::app.module.browser.type.page.@this page)
            return data.@this<type.window.@this>.From(Context.Error(new global::app.error.ActionError($"No page to open: Url is {Url.Peek()}", "UrlMissing", 400)));
        // waiting before it opens: its title may come first, and pairs it
        var window = onScreen.window.Opening();
        var opened = await onScreen.Open(page, Context);
        if (opened.Success) return Context.Ok<type.window.@this>(window);
        onScreen.window.Unopened(window);
        return data.@this<type.window.@this>.From(opened);
    }
}
