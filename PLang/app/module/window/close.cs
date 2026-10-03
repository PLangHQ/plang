using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>Closes a window: its page closes, and the window with it.</summary>
[Action("close", Cacheable = false)]
public partial class close : IContext
{
    /// <summary>The window: one from <c>window.open</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await type.window.@this.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        await window.Close();
        return Context.Ok();
    }
}
