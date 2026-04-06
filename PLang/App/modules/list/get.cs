using App.Variables;

namespace App.modules.list;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial int Index { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(App.Data.@this.FromError(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Index < 0 || Index >= list.Count)
            return Task.FromResult(App.Data.@this.FromError(
                new App.Errors.ValidationError($"Index {Index} out of range (0..{list.Count - 1})")));

        return Task.FromResult(App.Data.@this.Ok(list[Index]));
    }
}
