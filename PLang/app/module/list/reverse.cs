using app.variable;

namespace app.module.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw(Context.Variable.Get(ListName.Materialize() as app.variable.@this).Materialize(), Context);
        if (nl == null)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Materialize()}' is not a list")));
        // Promote to native (no-op when already native) so the in-place reverse persists.
        Context.Variable.Set(ListName.Materialize() as app.variable.@this, nl);

        nl.Reverse();
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list")));
    }
}
