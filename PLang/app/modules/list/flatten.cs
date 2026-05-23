using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Recursively flatten nested lists into a single flat list")]
[Action("flatten")]
public partial class Flatten : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        var existing = Context.Variables.Get(ListName.Value).Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(global::app.data.@this<types.list>.FromError(
                new app.errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var result = new List<object?>();
        FlattenRecursive(list, result);

        return Task.FromResult(global::app.data.@this<types.list>.Ok(new types.list { count = result.Count, value = result }, app.data.type.FromName("list")));
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
