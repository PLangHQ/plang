using app.variable;

namespace app.module.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw((await (await Context.Variable.Get((await ListName.Value()))).Value()), Context);
        if (nl == null)
            return Context.Error<type.list>(
                new app.error.ValidationError($"Variable '{(await ListName.Value())}' is not a list"));
        // Promote to native (no-op when already native) so the in-place reverse persists.
        await Context.Variable.Set((await ListName.Value()), nl);

        nl.Reverse();
        return Context.Ok<type.list>(new type.list { count = nl.CountRaw, value = nl }, Context.Type.Create("list"));
    }
}
