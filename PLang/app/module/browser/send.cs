namespace app.module.browser;

/// <summary>
/// Gives a running browser (from <c>browser.start</c>) what the person did: an input — the mouse moved, clicked or
/// wheeled, a key went down or up, text was typed, back, forward or reload was asked (what a screen's OnInput hands
/// over as <c>%!data%</c>: <c>send %!data% to %browser%</c>).
/// </summary>
[Action("send", Cacheable = false)]
public partial class send : IContext
{
    /// <summary>The input: the mouse, a key, typed text, or back/forward/reload.</summary>
    public partial data.@this<global::app.type.item.input.@this> Data { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<type.browser.@this> Browser { get; init; }

    public async Task<data.@this> Start()
    {
        if (await Browser.Value() is not { } browser)
            return Context.Error(new global::app.error.ActionError("The browser isn't running.", "BrowserNotRunning", 409));
        if (await Data.Value() is not { } input)
            return Data.Success ? Context.Error(new global::app.error.ActionError("No input to send.", "InputMissing", 400)) : Data;
        return await browser.Send(input, Context);
    }
}
