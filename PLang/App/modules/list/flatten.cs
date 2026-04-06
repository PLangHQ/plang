using App.Engine.Variables;

namespace App.modules.list;

[Action("flatten")]
public partial class Flatten : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(Data.FromError(
                new App.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var result = new List<object?>();
        FlattenRecursive(list, result);

        return Task.FromResult(Data.Ok(new types.list { count = result.Count, value = result }, App.Engine.Variables.Type.FromName("list")));
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
