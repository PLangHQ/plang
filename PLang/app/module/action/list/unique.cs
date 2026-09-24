using app.variable;

namespace app.module.action.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Run()
    {
        var name = await ListName.Value();
        if (await (await Context.Variable.Get(name)).Value() is not app.type.item.list.@this nl)
            return Context.Error<app.type.item.list.@this>(
                new app.error.ValidationError($"Variable '{name}' is not a list"));

        // Dedup through the one compare path's structural equality — so a list of
        // equivalent dicts collapses to one (reference-equality HashSet would not).
        // Accumulate in a plain list so the inner scan doesn't re-walk the list's elements on every
        // outer iteration.
        var kept = new List<global::app.data.@this>();
        foreach (var item in nl.Items(Context))
        {
            bool dup = false;
            foreach (var k in kept)
                if (await item.Compare(k) == global::app.data.Comparison.Equal) { dup = true; break; }
            if (!dup) kept.Add(item);
        }
        return Context.Ok(new app.type.item.list.@this(kept));
    }
}
