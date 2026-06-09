using app.variable;

namespace app.module.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.number.@this> AtIndex { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var listName = (await ListName.Value());
        var data = Context.Variable.Get(listName);
        var existing = (await data.Value());
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
            Context.Variable.Set(listName, list);
        }

        // A list value is structure-copied so the target doesn't alias the source
        // variable — `add %b% to %a%` then set/remove/insert on either must stay
        // independent (merge semantics, like extend). A scalar/dict element is stored by
        // reference: Stage 2's rebind means `set %x% = ...` mints a new Data rather than
        // mutating the one the list holds, so no defensive copy is needed there.
        data.@this toAdd = (await Value.Value()) is app.type.list.@this nl
            ? new data.@this(Value.Name, nl.CopyStructure(), Value.Type) { Context = Context }
            : Value;

        if (AtIndex.GetValue<int>() >= 0 && AtIndex.GetValue<int>() <= list.Count)
            list.Insert(AtIndex.GetValue<int>(), toAdd);
        else
            list.Add(toAdd);

        return global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list"));
    }
}
