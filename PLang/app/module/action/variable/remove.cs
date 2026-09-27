namespace app.module.action.variable;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<app.type.item.variable.@this> Name { get; init; }

    public async Task<data.@this> Start()
    {
        // a refusal (what is bound before the remove) is the action's answer
        var removed = await Context.Variable.Remove(await Name.Value());
        return removed.Success ? Data() : removed;
    }
}
