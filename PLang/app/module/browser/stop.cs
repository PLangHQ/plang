using app.module.browser.code;

namespace app.module.browser;

/// <summary>Ends a running browser (from <c>browser.start</c>).</summary>
[Action("stop", Cacheable = false)]
public partial class stop : IContext
{
    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<type.browser.@this> Browser { get; init; }

    public async Task<data.@this> Start()
    {
        if (await Browser.Value() is { } browser) await browser.Stop();
        return Context.Ok();
    }
}
