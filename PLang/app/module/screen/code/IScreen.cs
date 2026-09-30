using app.module.code;

namespace app.module.screen.code;

/// <summary>Screen provider: a Win32 window that shows frames (Windows, <see cref="Default"/>), or
/// PlangOS's display that makes them (Linux, <see cref="Wayland"/>).</summary>
public interface IScreen : ICode
{
    Task<data.@this<Screen>> Open(open action);
    Task<data.@this> Draw(draw action);
    Task<data.@this> Send(send action);
    Task<data.@this> Close(close action);
}
