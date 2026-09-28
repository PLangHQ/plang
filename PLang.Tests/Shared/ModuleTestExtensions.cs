namespace PLang.Tests;

/// <summary>Test reads over the app's modules.</summary>
public static class ModuleTestExtensions
{
    /// <summary>The module <paramref name="name"/> names — the app's own; for a name the app has no
    /// module for, one outside the app holding no actions (the unknown module a test's action names).</summary>
    public static global::app.module.@this Module(this global::app.@this app, string name)
        => app.module.list.Items().FirstOrDefault(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase))
           ?? new global::app.module.@this(name, app.module);
}
