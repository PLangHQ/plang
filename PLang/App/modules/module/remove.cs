using App;
using App.Variables;

namespace App.modules.module;

/// <summary>
/// Unregisters all actions for a module by name. Returns 404 if the module is not found.
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    public partial string Name { get; init; }

    public Task<Data.@this> Run()
    {
        var app = Context.App!;
        if (!app.Modules.Contains(Name))
            return Task.FromResult(Error(
                new App.Errors.ServiceError($"Module '{Name}' not found", "NotFound", 404)));

        app.Modules.Remove(Name);
        return Task.FromResult(Data());
    }
}
