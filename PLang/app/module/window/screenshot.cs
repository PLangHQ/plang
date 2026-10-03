using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>
/// How a window's page looks now: a PNG image — what an agent looks at to see what it changed.
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
        var window = await type.window.@this.Of(Window, Browser, Context);
        return await window.Value() is { } shown ? await shown.Screenshot(Context) : window;
    }
}
