using app.error;
using FilePath = app.type.item.path.file.@this;
using Display = app.module.screen.type.screen.display.@this;

namespace app.module.browser.code;

/// <summary>
/// The browser is Chromium: on a screen (PlangOS's display) a normal browser drawing onto it, else headless — see
/// <see cref="type.browser.screen.@this"/> and <see cref="type.browser.headless.@this"/>. Chromium is the system's own
/// browser, started by this module (capability "browser"), not a program the goal names — so no execute prompt: the
/// app's input may be a pipe (PlangOS), where a prompt would take a line meant for the browser.
/// </summary>
public sealed class Chromium : IBrowser
{
    public string Name { get; init; } = "chromium";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private static readonly string[] Programs = ["chromium", "chromium-browser", "google-chrome", "//usr/lib/chromium/chromium"];

    public async Task<data.@this<type.browser.@this>> Start(start action)
    {
        var context = action.Context;
        var program = Programs.Select(p => FilePath.Program(p, context)).FirstOrDefault(p => p != null);
        if (program == null)
            return data.@this<type.browser.@this>.From(context.Error(new ActionError(
                "No Chromium found (chromium on PATH, or /usr/lib/chromium/chromium).", "BrowserNotFound", 404)));

        var url = (await action.Url.Value())!.Clr<string>()!;
        if (action.Screen != null && await action.Screen.Value() is Display display)
            return await type.browser.screen.@this.Start(program, display, url,
                action.OnMessage == null ? null : await action.OnMessage.Value(), context);
        return await type.browser.headless.@this.Start(program, url,
            (int)(await action.Width.Value())!.ToDouble(), (int)(await action.Height.Value())!.ToDouble(),
            (await action.Format.Value())!.Clr<string>()!, (int)(await action.Quality.Value())!.ToDouble(),
            action.OnFrame == null ? null : await action.OnFrame.Value(), context);
    }
}
