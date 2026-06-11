using app.variable;

namespace app.module.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.number.@this> Index { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw((await (await Context.Variable.Get((await ListName.Value()))).Value()), Context);
        if (nl == null)
            return global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{(await ListName.Value())}' is not a list"));
        // Promote to native (no-op when already native) so the in-place set persists.
        await Context.Variable.Set((await ListName.Value()), nl);

        // Typed read — the index is a number end to end; the list lowers it
        // inside its own index-math boundary.
        var index = (await Index.Value())!;
        if (index < 0 || index >= nl.Count)
        {
            var lastIndex = nl.Count - 1;   // number arithmetic; renders via its own ToString
            return global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Index {index} out of range (0..{lastIndex})"));
        }
        // The slot mints its OWN Data pointing at the value's current instance
        // — reference semantics, same rule as list.add (nothing copied).
        global::app.data.@this item = Value == null
            ? new global::app.data.@this("", null)
            : new global::app.data.@this(Value.Name, await Value.Value(), Value.Type) { Context = Context };
        nl.SetAt(index, item);
        return global::app.data.@this<type.list>.Ok(new type.list { count = nl.CountRaw, value = nl }, app.type.@this.FromName("list"));
    }
}
