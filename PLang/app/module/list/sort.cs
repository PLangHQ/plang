namespace app.module.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Descending { get; init; }
    /// <summary>Optional element field to sort by — `sort %people% by "age"`. Sorts by element value when absent.</summary>
    public partial data.@this<global::app.type.item.text.@this>? By { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await ListName.Use(name => name.Change<app.type.item.list.@this>(Context, list => Descending.Use(async down =>
        {
            // An optional field: absent, or written as nothing, sorts by the elements themselves.
            var by = By == null ? null : await By.Given();
            if (by is { Success: false }) return by;
            var field = by?.Peek()?.ToString();
            return await list.Sort(field == null ? null : (global::app.type.item.text.@this)field, down, Context);
        }))));
}
