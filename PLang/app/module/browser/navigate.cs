using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Sends one of the browser's windows (by the id plang-screen gave it) to a page: an address as typed
/// (<c>mbl.is</c> becomes <c>https://mbl.is</c>), or words to search for.
/// </summary>
[Action("navigate", Cacheable = false)]
public partial class navigate : IContext
{
    /// <summary>The window, by its id from the window events.</summary>
    public partial data.@this<global::app.type.item.number.@this> Window { get; init; }

    /// <summary>Where to go: an address, or words to search for.</summary>
    public partial data.@this<global::app.type.item.text.@this> Url { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.Navigate(this);
}
