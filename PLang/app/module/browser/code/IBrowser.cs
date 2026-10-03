using app.module.code;

namespace app.module.browser.code;

/// <summary>Browser provider: starts the browser this system has (<see cref="Chromium"/>). What a browser does once
/// started is its own (<see cref="type.browser.@this"/>).</summary>
public interface IBrowser : ICode
{
    Task<data.@this<type.browser.@this>> Start(start action);
}
