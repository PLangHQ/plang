using app.variable;

namespace app.module.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<int> AtIndex { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var existing = data.Value;
        var list = existing as app.type.list.@this;

        if (list == null)
        {
            // Promote a non-list (or legacy raw list) value into the native list type.
            list = new app.type.list.@this { Context = Context };
            if (existing is app.type.list.@this) { /* unreachable — the cast above hit */ }
            else if (existing is System.Collections.IEnumerable seq && existing is not string)
                foreach (var item in seq)
                    list.Add(item as data.@this ?? new data.@this("", item));
            else if (existing != null)
                list.Add(existing as data.@this ?? new data.@this("", existing));
            Context.Variable.Set(ListName.Value, list);
        }

        // Store the element Data by reference — no clone. Stage 2's rebind means
        // `set %x% = ...` mints a new Data rather than mutating the one the list
        // holds, so the captured element stays untouched without a defensive copy.
        if (AtIndex.Value >= 0 && AtIndex.Value <= list.Count)
            list.Insert(AtIndex.Value, Value);
        else
            list.Add(Value);

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
