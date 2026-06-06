using app.variable;

namespace app.module.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.number.@this> AtIndex { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw(Context.Variable.Get(ListName.Value).Value, Context);
        if (nl == null)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));
        // Promote to native (no-op when already native) so the in-place remove persists.
        Context.Variable.Set(ListName.Value, nl);

        if (AtIndex.GetValue<int>() >= 0) nl.RemoveAt(AtIndex.GetValue<int>());
        else nl.Remove(Value.Value);
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list")));
    }
}
