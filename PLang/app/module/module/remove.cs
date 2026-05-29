using app;
using app.variable;

namespace app.module.module;

/// <summary>
/// Unregisters all actions for a module by name. Returns 404 if the module is not found.
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    public partial data.@this<string> Name { get; init; }

    public Task<data.@this> Run()
    {
        var app = Context.App!;
        if (!app.Module.Contains(Name.Value!))
            return Task.FromResult(Error(
                new app.error.ServiceError($"Module '{Name.Value}' not found", "NotFound", 404)));

        app.Module.Remove(Name.Value!);
        return Task.FromResult(Data());
    }
}
