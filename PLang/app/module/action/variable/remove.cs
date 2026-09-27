namespace app.module.action.variable;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<app.type.item.variable.@this> Name { get; init; }

    public async Task<data.@this> Start()
    {
        Context.Variable.Remove(await Name.Value());
        return Data();
    }
}
