using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.module;

/// <summary>
/// Unregisters all actions for a module by name. Returns 404 if the module is not found.
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        var engine = Context.Engine!;
        if (!engine.Modules.Contains(Name))
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"Module '{Name}' not found", "NotFound", 404)));

        engine.Modules.Remove(Name);
        return Task.FromResult(Data.Ok());
    }
}
