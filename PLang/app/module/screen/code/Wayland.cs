using Display = app.module.screen.type.screen.display.@this;
using Shows = app.module.screen.type.screen.view.@this;

namespace app.module.screen.code;

/// <summary>The Linux screen provider: inside PlangOS, a display that programs draw onto and whose frames go to this
/// app's output (<c>ToOutput</c>) — see <see cref="Display"/>; on a Linux host, a view that shows what PlangOS sends,
/// as the Windows host's window does — see <see cref="Shows"/>.</summary>
public sealed class Wayland : IScreen
{
    public string Name { get; init; } = "wayland";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<type.screen.@this>> Open(open action)
    {
        var title = (await action.Title.Value())?.ToString() ?? "";
        int width = (int)(await action.Width.Value())!.ToDouble(), height = (int)(await action.Height.Value())!.ToDouble();
        if ((await action.ToOutput.Value())!.Value)
            return await Display.Start(title, width, height, true,
                action.OnWindow == null ? null : await action.OnWindow.Value(), action.Context);
        return Shows.Start(title, width, height,
            action.OnInput == null ? null : await action.OnInput.Value(),
            action.OnClose == null ? null : await action.OnClose.Value(), action.Context);
    }
}
