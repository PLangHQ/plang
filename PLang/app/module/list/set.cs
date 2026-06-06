using app.variable;

namespace app.module.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.number.@this> Index { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw(Context.Variable.Get(ListName.Value).Value, Context);
        if (nl == null)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));
        // Promote to native (no-op when already native) so the in-place set persists.
        Context.Variable.Set(ListName.Value, nl);

        if (Index.GetValue<int>() < 0 || Index.GetValue<int>() >= nl.Count)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Index {Index.Value} out of range (0..{nl.Count - 1})")));
        // A list value is structure-copied so the slot doesn't alias the source variable
        // (same reason as list.add); scalars/dicts are stored by reference (rebind-safe).
        global::app.data.@this item = Value?.Value is app.type.list.@this nlv
            ? new global::app.data.@this(Value.Name, nlv.CopyStructure(), Value.Type) { Context = Context }
            : Value ?? new global::app.data.@this("", null);
        nl.SetAt(Index.GetValue<int>(), item);
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list")));
    }
}
