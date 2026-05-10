using System.Reflection;
using System.Runtime.Loader;

namespace App.Code;

/// <summary>
/// Collectible ALC for runtime-loaded scripts and DLLs. Falls back to the
/// default load context for every assembly so the loaded code shares Type
/// identity with the host (Task, Context, Data, etc.).
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    public PluginLoadContext() : base(isCollectible: true) { }
    public PluginLoadContext(string name) : base(name, isCollectible: true) { }

    protected override Assembly? Load(AssemblyName name) => null;
}
