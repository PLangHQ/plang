using App.Variables;
using App.Code;

namespace App.modules.ui.code;

/// <summary>
/// Template rendering provider. Swappable via app.Code.
/// Default implementation uses Fluid (Liquid syntax).
/// </summary>
public interface ITemplate : ICode
{
    /// <summary>Renders the template described by the action and returns the output string as Data.</summary>
    Task<Data.@this> Render(Render action);
}
