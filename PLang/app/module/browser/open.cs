using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Opens a page in its own window of a running browser (from <c>browser.start</c>): an app window,
/// with a title bar and no tabs or address bar. On plang-screen it is one more window on the screen,
/// which the window events (OnWindow) then report.
/// </summary>
[Action("open", Cacheable = false)]
public partial class open : IContext
{
    /// <summary>The page to open.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.Open(this);
}
