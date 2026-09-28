namespace app.module.action.module;

/// <summary>
/// Unregisters all actions for a module by name. A module not there is Get's own NotFound.
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    /// <summary>Module name to unregister (e.g., "mymodule").</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    public Task<data.@this> Start() => Name.Use(async name =>
        await (await Context.App.module.Get(name.ToString())).Use<global::app.module.@this>(async module =>
        {
            await Context.App.module.list.Remove(module, Context);
            // authoritative: anyone still holding the module finds it empty
            module.Clear();
            return Context.Ok();
        }));
}
