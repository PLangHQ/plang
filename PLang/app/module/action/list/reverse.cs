using app.variable;

namespace app.module.action.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var name = await ListName.Value();
        if (await (await Context.Variable.Get(name)).Value() is not app.type.item.list.@this nl)
            return Context.Error<type.list>(
                new app.error.ValidationError($"Variable '{name}' is not a list"));
        // Persist the retrieved instance so the in-place reverse sticks.
        await Context.Variable.Set(name, nl);

        nl.Reverse();
        return Context.Ok<type.list>(new type.list { count = nl.CountRaw, value = nl }, Context.Type.Create("list"));
    }
}
