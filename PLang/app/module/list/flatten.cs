using app.variable;

namespace app.module.list;

[Action("flatten")]
public partial class Flatten : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var existing = Context.Variable.Get(ListName.Value).Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var result = new List<object?>();
        FlattenRecursive(list, result);

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = result.Count, value = result }, app.type.@this.FromName("list")));
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
