using System.Reflection;
using app.errors;
using app.variables;
using app.modules.code;

namespace app.modules.code;

/// <summary>
/// Loads a provider from a DLL or registers a provider instance.
/// PLang: load provider 'my-crypto.dll' as 'custom-crypto'
/// </summary>
[Action("load", Cacheable = false)]
public partial class load : IContext
{
    /// <summary>Path to the DLL to load (relative to app root or absolute).</summary>
    public partial data.@this<global::app.types.path.@this>? Path { get; init; }

    /// <summary>Optional display name for the provider (not currently used — provider supplies its own Name).</summary>
    public partial data.@this<string>? Name { get; init; }

    public async Task<data.@this> Run()
    {
        var dllPath = Path?.Value;
        if (dllPath == null)
            return Error(new ActionError("Provider path is required", "ValidationError", 400));

        // LoadAssemblyAsync gates on Execute (Unix r/w/x model) — a user who
        // granted Read on the folder still gets a separate Execute prompt
        // before the DLL is loaded.
        var loadResult = await dllPath.LoadAssemblyAsync();
        if (!loadResult.Success) return Error(loadResult.Error!);
        var assembly = loadResult.Value!;

        var providerTypes = assembly.GetExportedTypes()
            .Where(t => typeof(ICode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (providerTypes.Count == 0)
            return Error(new ActionError("No ICode implementations found in assembly", "NoProviders", 400));

        var registered = new List<ICode>();
        foreach (var type in providerTypes)
        {
            var ctor = type.GetConstructor(System.Type.EmptyTypes);
            if (ctor == null)
                return Error(new ActionError($"Provider '{type.Name}' has no parameterless constructor", "ProviderConstructor", 400));

            var instance = (ICode)ctor.Invoke(null);
            // Stamp DLL origin so snapshot capture / restore can reload from the same source.
            instance.Source = dllPath.Absolute;

            // Register for each ICode-derived interface the type implements
            var interfaces = type.GetInterfaces()
                .Where(i => typeof(ICode).IsAssignableFrom(i) && i != typeof(ICode))
                .ToList();

            foreach (var iface in interfaces)
            {
                var result = Context.App.Code.Register(iface, instance);
                if (!result.Success) return result;
            }

            registered.Add(instance);
        }

        return Data(registered);
    }
}
