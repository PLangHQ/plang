using app.variable;

namespace app.module.action.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.item.number.@this> Index { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var name = await ListName.Value();
        if (await (await Context.Variable.Get(name)).Value() is not app.type.item.list.@this nl)
            return Context.Error<type.list>(
                new app.error.ValidationError($"Variable '{name}' is not a list"));
        // Persist the retrieved instance so the in-place set sticks.
        await Context.Variable.Set(name, nl);

        // Typed read — the index is a number end to end; the list lowers it
        // inside its own index-math boundary.
        var index = (await Index.Value())!;
        if (index < 0 || index >= nl.Count)
        {
            var lastIndex = nl.Count - 1;   // number arithmetic; renders via its own ToString
            return Context.Error<type.list>(
                new app.error.ValidationError($"Index {index} out of range (0..{lastIndex})"));
        }
        // The slot mints its OWN Data pointing at the value's current instance
        // — reference semantics, same rule as list.add (nothing copied).
        global::app.data.@this item = Value == null
            ? new global::app.data.@this("", null, context: Context)
            : new global::app.data.@this(Value.Name, await Value.Value(), Value.Type, context: Context);
        nl.SetAt(index, item);
        return Context.Ok<type.list>(new type.list { count = nl.CountRaw, value = nl }, Context.Type.Create("list"));
    }
}
