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
        // A list value is structure-copied so the slot doesn't alias the source variable
        // (same reason as list.add); scalars/dicts are stored by reference (rebind-safe).
        global::app.data.@this item = (Value == null ? null : await Value.Value()) is app.type.list.@this nlv
            ? new global::app.data.@this(Value.Name, nlv.CopyStructure(), Value.Type) { Context = Context }
            : Value ?? new global::app.data.@this("", null);
        nl.SetAt(index, item);
        return global::app.data.@this<type.list>.Ok(new type.list { count = nl.CountRaw, value = nl }, app.type.@this.FromName("list"));
    }
}
