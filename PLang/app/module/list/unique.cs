using app.variable;

namespace app.module.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var existing = Context.Variable.Get(ListName.Value).Value;
        if (existing is app.type.list.@this nl)
        {
            // Dedup through the one compare path's structural equality — so a list of
            // equivalent dicts collapses to one (reference-equality HashSet would not).
            var deduped = new app.type.list.@this { Context = Context };
            foreach (var item in nl.Items)
            {
                bool dup = false;
                foreach (var kept in deduped.Items)
                    if (global::app.data.Compare.AreEqual(item, kept)) { dup = true; break; }
                if (!dup) deduped.Add(item);
            }
            return Task.FromResult(global::app.data.@this<type.list>.Ok(
                new type.list { count = deduped.Count, value = deduped }, app.type.@this.FromName("list")));
        }
        if (existing is not List<object?> list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(global::app.data.@this<type.list>.Ok(
            new type.list { count = distinct.Count, value = distinct }, app.type.@this.FromName("list")));
    }
}
