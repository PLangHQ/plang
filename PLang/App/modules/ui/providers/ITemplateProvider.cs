using App.Variables;
using App.Providers;

namespace App.modules.ui.providers;

/// <summary>
/// Template rendering provider. Swappable via app.Providers.
/// Default implementation uses Fluid (Liquid syntax).
/// </summary>
public interface ITemplateProvider : IProvider
{
    /// <summary>Renders the template described by the action and returns the output string as Data.</summary>
    Task<Data.@this> Render(Render action);
}
