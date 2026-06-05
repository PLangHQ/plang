using app.variable;

namespace app.module.list;

[Action("flatten")]
public partial class Flatten : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var existing = Context.Variable.Get(ListName.Value).Value;
        if (existing is app.type.list.@this nl)
        {
            var flat = new app.type.list.@this { Context = Context };
            FlattenNative(nl, flat);
            return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = flat.Count, value = flat }, app.type.@this.FromName("list")));
        }
        if (existing is not System.Collections.IList list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var result = new List<object?>();
        FlattenRecursive(list, result);

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = result.Count, value = result }, app.type.@this.FromName("list")));
    }

    // Flatten a native list: a nested-list element's elements are lifted; any other
    // element Data is kept as-is (its own type-tag preserved).
    private static void FlattenNative(app.type.list.@this source, app.type.list.@this target)
    {
        foreach (var item in source.Items)
        {
            if (item.Value is app.type.list.@this nested)
                FlattenNative(nested, target);
            else
                target.Add(item);
        }
    }

    private static void FlattenRecursive(System.Collections.IList source, List<object?> target)
    {
        foreach (var item in source)
        {
            if (item is System.Collections.IList nested && item is not string)
                FlattenRecursive(nested, target);
            else
                target.Add(item);
        }
    }
}
