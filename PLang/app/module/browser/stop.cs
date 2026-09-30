using app.module.browser.code;

namespace app.module.browser;

/// <summary>Ends a running browser (from <c>browser.start</c>).</summary>
[Action("stop", Cacheable = false)]
public partial class stop : IContext
{
    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.Stop(this);
}
