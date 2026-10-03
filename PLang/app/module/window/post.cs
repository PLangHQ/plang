using Browser = app.module.browser.type.browser.@this;

namespace app.module.window;

/// <summary>
/// Gives a window's page a message: the page gets a <c>message</c> event whose <c>origin</c> is
/// <c>"plang"</c> and whose <c>data</c> is the message as text (a dict or list as its json). The page
/// answers through <c>plang(text)</c>, which the browser's OnMessage receives.
/// </summary>
[Action("post", Cacheable = false)]
public partial class post : IContext
{
    /// <summary>The message.</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The window: one from <c>window.open</c>, <c>%browser.desktop%</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await type.window.@this.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        // the Data writes itself as text
        using var text = new MemoryStream();
        await global::app.type.item.text.@this.Encode(text, Data, Context, null, null, CancellationToken.None);
        await window.Post(System.Text.Encoding.UTF8.GetString(text.ToArray()));
        return Context.Ok();
    }
}
