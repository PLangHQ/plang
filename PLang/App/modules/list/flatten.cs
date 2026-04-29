using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Recursively flatten nested lists into a single flat list")]
[Action("flatten")]
public partial class Flatten : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName).Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var result = new List<object?>();
        FlattenRecursive(list, result);

        return Task.FromResult(Data(new types.list { count = result.Count, value = result }, App.Data.Type.FromName("list")));
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
