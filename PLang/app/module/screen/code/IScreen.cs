using app.module.code;

namespace app.module.screen.code;

/// <summary>Screen provider: opens the screen this system has — a window on the host (Windows, <see cref="Default"/>),
/// or PlangOS's display (Linux, <see cref="Wayland"/>). What a screen does once open is its own
/// (<see cref="type.screen.@this"/>).</summary>
public interface IScreen : ICode
{
    Task<data.@this<type.screen.@this>> Open(open action);
}
