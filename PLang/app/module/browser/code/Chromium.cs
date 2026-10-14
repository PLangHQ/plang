using Display = app.module.screen.type.screen.display.@this;

namespace app.module.browser.code;

/// <summary>
/// The browser is Chromium: on a screen (PlangOS's display) a normal browser drawing onto it, else headless — see
/// <see cref="type.browser.screen.@this"/> and <see cref="type.browser.headless.@this"/>. A goal of plang's own starts it
/// (<c>/system/browser/</c>, through terminal, trusted by its origin); DevTools speaks over its pipe.
/// </summary>
public sealed class Chromium : IBrowser
{
    public string Name { get; init; } = "chromium";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<type.browser.@this>> Start(start action)
    {
        var context = action.Context;
        // the page as Chromium's address: a plang path (/system/plangos/desktop.html) is that file's
        var url = type.browser.@this.Where((await action.Url.Value())!.Clr<string>()!, context);
        if (action.Screen != null && await action.Screen.Value() is Display display)
            return await type.browser.screen.@this.Start(display, url,
                action.OnMessage == null ? null : await action.OnMessage.Value(), context);
        return await type.browser.headless.@this.Start(url,
            (int)(await action.Width.Value())!.ToDouble(), (int)(await action.Height.Value())!.ToDouble(),
            (await action.Format.Value())!.Clr<string>()!, (int)(await action.Quality.Value())!.ToDouble(),
            action.OnFrame == null ? null : await action.OnFrame.Value(), context);
    }
}
