using app;
using app.Variables;

namespace app.modules.module;

/// <summary>
/// Unregisters all actions for a module by name. Returns 404 if the module is not found.
/// </summary>
[System.ComponentModel.Description("Unregister all actions of a named module from the current app's action catalog")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    public partial Data.@this<string> Name { get; init; }

    public Task<Data.@this> Run()
    {
        var app = Context.App!;
        if (!app.Modules.Contains(Name.Value!))
            return Task.FromResult(Error(
                new app.Errors.ServiceError($"Module '{Name.Value}' not found", "NotFound", 404)));

        app.Modules.Remove(Name.Value!);
        return Task.FromResult(Data());
    }
}
