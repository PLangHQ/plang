using app.variable;
using app.modules.code;

namespace app.modules.ui.code;

/// <summary>
/// Template rendering provider. Swappable via app.Code.
/// Default implementation uses Fluid (Liquid syntax).
/// </summary>
public interface ITemplate : ICode
{
    /// <summary>Renders the template described by the action and returns the output string as Data.</summary>
    Task<data.@this<string>> Render(Render action);
}
