using Display = app.module.screen.type.screen.display.@this;

namespace app.module.screen.code;

/// <summary>PlangOS's screen provider (Linux): a display that programs draw onto — see <see cref="Display"/>.</summary>
public sealed class Wayland : IScreen
{
    public string Name { get; init; } = "wayland";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<type.screen.@this>> Open(open action)
        => await Display.Start((await action.Title.Value())?.ToString() ?? "",
            (int)(await action.Width.Value())!.ToDouble(), (int)(await action.Height.Value())!.ToDouble(),
            (await action.ToOutput.Value())!.Value,
            action.OnWindow == null ? null : await action.OnWindow.Value(), action.Context);
}
