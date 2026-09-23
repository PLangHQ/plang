using app.variable;

namespace app.module.action.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.item.number.@this> AtIndex { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var listName = (await ListName.Value());
        var data = await Context.Variable.Get(listName);
        var existing = (await data.Value());
        var list = existing as app.type.item.list.@this;

        if (list == null)
        {
            // Promote a non-list (or legacy raw list) value into the native list type.
            list = new app.type.item.list.@this(Context);
            if (data.HasValue)
                list.Add(new data.@this("", existing, context: Context));
            await Context.Variable.Set(listName, list);
        }

        // The entry mints its OWN Data pointing at the value's current
        // instance — O(1), nothing copied. Collections are reference
        // semantics: `add %b% to %a%` shares %b%'s list instance (a later
        // in-place mutation of %b% is visible through %a%, like C#), while a
        // later `set %b% = ...` rebinds %b% and never touches the entry.
        var value = await Value.Value();

        // Typed read — number end to end; the list lowers inside its own boundary.
        var atIndex = (await AtIndex.Value())!;
        var positioned = atIndex >= 0 && atIndex <= list.Count;
        if (value is app.type.item.list.@this items)
        {
            // Adding a list EXTENDS: its elements join this list (an O(1) chunk, nothing copied).
            if (positioned) list.Insert(atIndex, items);
            else list.Add(items);
        }
        else
        {
            data.@this toAdd = new data.@this(Value.Name, value, Value.Type, context: Context);
            if (positioned) list.Insert(atIndex, toAdd);
            else list.Add(toAdd);
        }

        return Context.Ok<type.list>(new type.list { count = list.CountRaw, value = list }, Context.Type.Create("list"));
    }
}
