using app.variable;

namespace app.module.action.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Run()
    {
        var name = await ListName.Value();
        var held = await Context.Variable.Get(name);
        if (await held.Value() is not app.type.item.list.@this nl)
            return Context.Error<app.type.item.list.@this>(
                new app.error.ValidationError($"Variable '{name}' is not a list"));
        // Persist the retrieved instance so the in-place reverse sticks — unless a newer value
        // took the name in between.
        await Context.Variable.Replace(name, held, nl);

        nl.Reverse();
        return Context.Ok(nl);
    }
}
