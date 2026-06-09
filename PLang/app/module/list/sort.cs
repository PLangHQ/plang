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

    public async Task<data.@this<type.list>> Run()
    {
        var listName = (await ListName.Value())!;
        var nl = app.type.list.@this.FromRaw(await (await Context.Variable.Get(listName)).Value(), Context);
        if (nl == null)
            return global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{listName}' is not a list"));
        // Promote the variable to the native list (no-op when already native) so the
        // in-place sort persists — mirrors list.add's raw→native promotion.
        await Context.Variable.Set(listName, nl);

        // Thin dispatch — the list value type owns ordering, routed through the
        // one typed-compare path. `by "field"` keys each element. An unorderable
        // element (a dict, a mixed-type list) is an EXPECTED data condition — in PLang
        // we return it as an error so `on error …` can catch it, never throw (a thrown
        // exception is for the unexpected, and escapes the error-handler pipeline).
        bool descending = (await Descending.Value())?.Value ?? false;
        string? by = By == null ? null : (await By.Value())?.ToString();
        try
        {
            if (!string.IsNullOrEmpty(by))
                await nl.SortByField(by, descending);
            else
                nl.SortByValue(descending);
        }
        catch (global::app.data.Compare.NotOrderableException ex)
        {
            return global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError(ex.Message));
        }
        return global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list"));
    }
}
