using app.error;

namespace app.module.screen.code;

/// <summary>Default screen provider: a window on the host (Windows) — see <see cref="type.screen.window.@this"/>.</summary>
public sealed class Default : IScreen
{
    public string Name { get; init; } = "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<type.screen.@this>> Open(open action)
    {
        var context = action.Context;
        if (!OperatingSystem.IsWindows())
            return data.@this<type.screen.@this>.From(context.Error(new ActionError("screen.open needs Windows for now.", "NotSupported", 400)));

        return type.screen.window.@this.Start((await action.Title.Value())!.Clr<string>()!,
            (int)(await action.Width.Value())!.ToDouble(), (int)(await action.Height.Value())!.ToDouble(),
            action.OnInput == null ? null : await action.OnInput.Value(),
            action.OnClose == null ? null : await action.OnClose.Value(), context);
    }
}
