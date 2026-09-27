using app;

namespace app.module.action.module;

/// <summary>
/// Unregisters all actions for a module by name. Returns 404 if the module is not found.
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    public async Task<data.@this> Start()
    {
        var app = Context.App;
        var found = await app.module.Get((await Name.Value())!.Clr<string>()!);
        if (!found.Success)
            return Error(
                new app.error.ServiceError($"Module '{(await Name.Value())}' not found", "NotFound", 404));

        // Authoritative: anyone still holding the module finds it empty.
        var module = (await found.Value())!;
        await app.module.list.Remove(module, Context);
        module.Clear();
        return Data();
    }
}
