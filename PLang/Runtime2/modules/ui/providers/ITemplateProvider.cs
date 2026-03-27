using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.ui.providers;

/// <summary>
/// Template rendering provider. Swappable via engine.Providers.
/// Default implementation uses Fluid (Liquid syntax).
/// </summary>
public interface ITemplateProvider : IProvider
{
    Task<Data> Render(Render action);
}
