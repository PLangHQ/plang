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
        var list = existing as List<object?>;

        if (list == null)
        {
            // Convert non-list value or create new list
            if (existing is System.Collections.IList rawList)
            {
                list = new List<object?>();
                foreach (var item in rawList) list.Add(item);
            }
            else if (existing != null)
            {
                list = new List<object?> { existing };
            }
            else
            {
                list = new List<object?>();
            }
            Context.Variable.Set(ListName.Value, list);
        }

        // Shallow-clone into its own Data so the entry is an independent binding —
        // value (lazy raw included), type and signature shared by reference, no
        // materialize, no deep clone. `set %x% = ...` replaces the binding (it never
        // mutates an aliased Data), so a later reassignment of %x% leaves the entry
        // untouched. Reference semantics for in-place mutation of a shared value object.
        data.@this snapshot = Value.ShallowClone(Value.Name);

        if (AtIndex.Value >= 0 && AtIndex.Value <= list.Count)
            list.Insert(AtIndex.Value, snapshot);
        else
            list.Add(snapshot);

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
