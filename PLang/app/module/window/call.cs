using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>
/// Calls one of a window's page's goals — a function of the page, like <c>ShowFiles</c> — the way
/// <c>goal.call</c> calls one of plang's: <c>call ShowFiles files=%files% in %window%</c> runs
/// <c>ShowFiles({files: …})</c> in the page, the arguments as one object, and what it returns (awaited)
/// is <c>%!data%</c>.
/// </summary>
[Action("call", Cacheable = false)]
public partial class call : IContext
{
    /// <summary>The page's goal: the name of a function the page has.</summary>
    [app.Attributes.Goal]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The arguments — one named row each, as for <c>goal.call</c>; the function gets them as
    /// one object, a property per name.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

    /// <summary>The window: one from <c>window.open</c>, <c>%browser.desktop%</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var window = await type.window.@this.Of(Window, Browser, Context);
        return await window.Value() is { } shown ? await shown.Call((await Name.Value())?.Clr<string>() ?? "", Parameter, Context) : window;
    }
}
