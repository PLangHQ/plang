using System.Reflection;
using App.Errors;
using App.Variables;
using App.Providers;

namespace App.modules.provider;

/// <summary>
/// Loads a provider from a DLL or registers a provider instance.
/// PLang: load provider 'my-crypto.dll' as 'custom-crypto'
/// </summary>
[Action("load", Cacheable = false)]
public partial class load : IContext
{
    /// <summary>Path to the DLL to load (relative to app root or absolute).</summary>
    public partial string? Path { get; init; }

    /// <summary>Optional display name for the provider (not currently used — provider supplies its own Name).</summary>
    public partial string? Name { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrEmpty(Path))
            return Data.FromError(new ActionError("Provider path is required", "ValidationError", 400));

        Assembly assembly;
        try
        {
            var fullPath = Context.App.FileSystem.Path.GetFullPath(Path, Context.App.AbsolutePath);
            assembly = Assembly.LoadFrom(fullPath);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "LoadError", 500));
        }

        var providerTypes = assembly.GetExportedTypes()
            .Where(t => typeof(IProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (providerTypes.Count == 0)
            return Data.FromError(new ActionError("No IProvider implementations found in assembly", "NoProviders", 400));

        var registered = new List<IProvider>();
        foreach (var type in providerTypes)
        {
            var ctor = type.GetConstructor(System.Type.EmptyTypes);
            if (ctor == null)
                return Data.FromError(new ActionError($"Provider '{type.Name}' has no parameterless constructor", "ProviderConstructor", 400));

            var instance = (IProvider)ctor.Invoke(null);

            // Register for each IProvider-derived interface the type implements
            var interfaces = type.GetInterfaces()
                .Where(i => typeof(IProvider).IsAssignableFrom(i) && i != typeof(IProvider))
                .ToList();

            foreach (var iface in interfaces)
            {
                var result = Context.App.Providers.Register(iface, instance);
                if (!result.Success) return result;
            }

            registered.Add(instance);
        }

        return Data.Ok(registered);
    }
}
