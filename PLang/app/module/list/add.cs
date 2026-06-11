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
        var data = await Context.Variable.Get(listName);
        var existing = (await data.Value());
        var list = existing as app.type.list.@this;

        if (list == null)
        {
            // Promote a non-list (or legacy raw list) value into the native list type.
            list = new app.type.list.@this { Context = Context };
            if (data.HasValue)
                list.Add(new data.@this("", existing));
            await Context.Variable.Set(listName, list);
        }

        // The entry mints its OWN Data pointing at the value's current
        // instance — O(1), nothing copied. Collections are reference
        // semantics: `add %b% to %a%` shares %b%'s list instance (a later
        // in-place mutation of %b% is visible through %a%, like C#), while a
        // later `set %b% = ...` rebinds %b% and never touches the entry.
        data.@this toAdd = new data.@this(Value.Name, await Value.Value(), Value.Type) { Context = Context };

        // Typed read — number end to end; the list lowers inside its own boundary.
        var atIndex = (await AtIndex.Value())!;
        if (atIndex >= 0 && atIndex <= list.Count)
            list.Insert(atIndex, toAdd);
        else
            list.Add(toAdd);

        return global::app.data.@this<type.list>.Ok(new type.list { count = list.CountRaw, value = list }, app.type.@this.FromName("list"));
    }
}
