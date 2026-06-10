using app.variable;

namespace app.module.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw((await (await Context.Variable.Get((await ListName.Value()))).Value()), Context);
        if (nl == null)
            return global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{(await ListName.Value())}' is not a list"));

        // Dedup through the one compare path's structural equality — so a list of
        // equivalent dicts collapses to one (reference-equality HashSet would not).
        // Accumulate in a plain list so the inner scan doesn't re-materialize Items
        // (a fresh flat List) on every outer iteration.
        var kept = new List<global::app.data.@this>();
        foreach (var item in nl.Items)
        {
            bool dup = false;
            foreach (var k in kept)
                if (await item.Compare(k) == global::app.data.Comparison.Equal) { dup = true; break; }
            if (!dup) kept.Add(item);
        }
        var deduped = new app.type.list.@this(kept) { Context = Context };
        return global::app.data.@this<type.list>.Ok(
            new type.list { count = deduped.CountRaw, value = deduped }, app.type.@this.FromName("list"));
    }
}
