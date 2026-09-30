using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Gives the browser's first page (the one <c>browser.start</c> opened) a message — or, with Window,
/// the page in that window, when it is one of the app's own: the page gets a <c>message</c> event
/// whose <c>origin</c> is <c>"plang"</c> and whose <c>data</c> is the message as text (a dict or list as
/// its json). The page answers through <c>plang(text)</c>, which OnMessage receives (from a window's
/// page with <c>"from"</c>: its window's id).
/// </summary>
[Action("post", Cacheable = false)]
public partial class post : IContext
{
    /// <summary>The message.</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    /// <summary>The window whose page gets it (the id OnMessage heard as <c>"from"</c>). None: the first page.</summary>
    public partial data.@this<global::app.type.item.number.@this>? Window { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.Post(this);
}
