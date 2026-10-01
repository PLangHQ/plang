using Browser = app.module.browser.Browser;

namespace app.module.window;

/// <summary>
/// A window's page loads again from its files, nothing cached: a change made to them shows at once.
/// </summary>
[Action("reload", Cacheable = false)]
public partial class reload : IContext
{
    /// <summary>The window: one from <c>window.open</c>, <c>%browser.desktop%</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await global::app.module.window.Window.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        await window.Reload();
        return Context.Ok();
    }
}
