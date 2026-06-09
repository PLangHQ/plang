using app.variable;

namespace app.module.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Descending { get; init; }
    /// <summary>Optional element field to sort by — `sort %people% by "age"`. Sorts by element value when absent.</summary>
    public partial data.@this<global::app.type.text.@this>? By { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw(Context.Variable.Get(ListName.Materialize() as app.variable.@this).Materialize(), Context);
        if (nl == null)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));
        // Promote the variable to the native list (no-op when already native) so the
        // in-place sort persists — mirrors list.add's raw→native promotion.
        Context.Variable.Set(ListName.Materialize() as app.variable.@this, nl);

        // Thin dispatch — the list value type owns ordering, routed through the
        // one typed-compare path. `by "field"` keys each element. An unorderable
        // element (a dict, a mixed-type list) is an EXPECTED data condition — in PLang
        // we return it as an error so `on error …` can catch it, never throw (a thrown
        // exception is for the unexpected, and escapes the error-handler pipeline).
        try
        {
            if (!string.IsNullOrEmpty(By?.Materialize()?.ToString()))
                nl.SortByField((By.Materialize() as global::app.type.text.@this)!, (Descending.Materialize() as global::app.type.@bool.@this)?.Value ?? false);
            else
                nl.SortByValue((Descending.Materialize() as global::app.type.@bool.@this)?.Value ?? false);
        }
        catch (global::app.data.Compare.NotOrderableException ex)
        {
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError(ex.Message)));
        }
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list")));
    }
}
