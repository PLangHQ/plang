using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// Context-bound view of a module's settings. Returned by engine.Settings.For&lt;T&gt;(context).
/// Each call to For&lt;T&gt; returns a new lightweight instance stamped with the current context,
/// so concurrent goals get isolated views. Properties resolve through the scope chain
/// via Engine.Settings.Resolve.
///
/// Usage:
///   var view = engine.Settings.For&lt;ArchiveSettings&gt;(context);
///   long max = view.Resolve&lt;long&gt;("max", defaultValue);
/// </summary>
public sealed class ModuleView<T> where T : ISettings, new()
{
    private readonly @this _settings;
    private readonly PLangContext _context;
    private readonly string _modulePrefix;

    public ModuleView(@this settings, PLangContext context, string modulePrefix)
    {
        _settings = settings;
        _context = context;
        _modulePrefix = modulePrefix;
    }

    /// <summary>
    /// Resolves a property value through the scope chain.
    /// Key is the property name (e.g., "max"), which gets prefixed with the module name.
    /// </summary>
    public TValue Resolve<TValue>(string propertyName, TValue classDefault)
    {
        throw new NotImplementedException();
    }
}
