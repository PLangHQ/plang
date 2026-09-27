namespace app.module.action.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Descending { get; init; }
    /// <summary>Optional element field to sort by — `sort %people% by "age"`. Sorts by element value when absent.</summary>
    public partial data.@this<global::app.type.item.text.@this>? By { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start()
    {
        var listName = (await ListName.Value())!;
        var held = await listName.Start(Context);
        if (await held.Value() is not app.type.item.list.@this nl)
            return Context.Error<app.type.item.list.@this>(
                new app.error.ValidationError($"Variable '{listName}' is not a list"));
        // Persist the retrieved instance so the in-place sort sticks — unless a newer value
        // took the name in between.
        await listName.Replace(held, nl, Context);

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
                await nl.SortByField(by, descending, Context);
            else
                await nl.SortByValue(descending, Context);
        }
        catch (global::app.data.IncomparableException ex)
        {
            return Context.Error<app.type.item.list.@this>(
                new app.error.ValidationError(ex.Message));
        }
        return Context.Ok(nl);
    }
}
