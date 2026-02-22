using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// Engine-level settings registry. Owns:
/// - Module type registration (which ISettings types exist)
/// - Engine-level default scope (persistent across goals)
/// - Resolution logic: context scope → parent scope → engine defaults → class defaults
///
/// Navigation: engine.Settings.For&lt;ArchiveSettings&gt;(context).Max
/// </summary>
public sealed class @this
{
    /// <summary>
    /// Engine-level default scope. Values here persist across goal executions.
    /// Written when a settings action has Default=true.
    /// </summary>
    public Scope Defaults { get; } = new();

    /// <summary>
    /// Resolves a setting value by walking the scope chain:
    /// context.SettingsScope → context.Parent.SettingsScope → ... → Defaults → classDefault.
    /// </summary>
    public T Resolve<T>(string key, PLangContext context, T classDefault)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a context-bound view for a specific ISettings type.
    /// The view resolves property values through the scope chain for the given context.
    /// </summary>
    public ModuleView<T> For<T>(PLangContext context) where T : ISettings, new()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes a setting value to the appropriate scope.
    /// If isDefault is true, writes to engine Defaults. Otherwise writes to the context's goal scope.
    /// </summary>
    public void Set(string key, object value, PLangContext context, bool isDefault = false)
    {
        throw new NotImplementedException();
    }
}
