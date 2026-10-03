using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>
/// Sends a window to a page: an address as typed (<c>mbl.is</c> becomes <c>https://mbl.is</c>), or
/// words to search for.
/// </summary>
[Action("navigate", Cacheable = false)]
public partial class navigate : IContext
{
    /// <summary>The window: one from <c>window.open</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>Where to go: an address, or words to search for.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await type.window.@this.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        if (await Url.Value() is not global::app.type.item.text.@this typed)
            return Context.Error(new global::app.error.ActionError($"Nowhere to go: Url is {Url.Peek()}", "UrlMissing", 400));
        return await window.Navigate(typed.Clr<string>() ?? "", Context) ?? Context.Ok();
    }
}
