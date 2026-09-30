using Browser = app.module.browser.Browser;

namespace app.module.window;

/// <summary>
/// Opens a page in a window of its own, in a running browser on the screen (from
/// <c>browser.start</c>): an app window, with PlangOS's title bar and no tabs. Returns the window at
/// once; it is shown when its page is, and what it is asked to do (navigate, post, callGoal) waits
/// for that.
/// </summary>
[Action("open", Cacheable = false)]
public partial class open : IContext
{
    /// <summary>The page to open.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    public async Task<data.@this<Window>> Start()
    {
        var browser = await Browser.Value();
        if (browser is not { Running: true, Screen: not null })
            return data.@this<Window>.From(Context.Error(new global::app.error.ActionError($"The browser can't open windows: {browser}", "BrowserNotRunning", 409)));
        if (await Url.Value() is not global::app.type.item.text.@this url)
            return data.@this<Window>.From(Context.Error(new global::app.error.ActionError($"No page to open: Url is {Url.Peek()}", "UrlMissing", 400)));
        var window = browser.Windows.Opening();
        global::app.module.browser.code.Chromium.Open(browser, url.Clr<string>() ?? "", Context);
        return Context.Ok<Window>(window);
    }
}
