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
        var window = await type.window.@this.Of(Window, Browser, Context);
        return await window.Value() is { } shown ? await shown.Close(Context) : window;
    }
}
