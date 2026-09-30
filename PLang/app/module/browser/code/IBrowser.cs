using app.module.code;

namespace app.module.browser.code;

/// <summary>
/// Browser provider. The default drives headless Chromium over DevTools; a compositor-based one
/// (plang-screen) can replace it behind the same actions and the same frame/input lines.
/// </summary>
public interface IBrowser : ICode
{
    Task<data.@this<Browser>> Start(start action);
    Task<data.@this> Send(send action);
    Task<data.@this> Stop(stop action);
}
