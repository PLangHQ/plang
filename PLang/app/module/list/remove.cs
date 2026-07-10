using app.variable;

namespace app.module.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.number.@this> AtIndex { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var listName = (await ListName.Value());
        var nl = app.type.list.@this.FromRaw((await (await Context.Variable.Get(listName)).Value()), Context);
        if (nl == null)
            return Context.Error<type.list>(
                new app.error.ValidationError($"Variable '{listName}' is not a list"));
        // Promote to native (no-op when already native) so the in-place remove persists.
        await Context.Variable.Set(listName, nl);

        // Typed read — number end to end; the list lowers inside its own boundary.
        var atIndex = (await AtIndex.Value())!;
        if (atIndex >= 0) nl.RemoveAt(atIndex);
        else await nl.Remove((await Value.Value()));
        return Context.Ok<type.list>(new type.list { count = nl.CountRaw, value = nl }, Context.Type.Create("list"));
    }
}
