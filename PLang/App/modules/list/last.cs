using App.Variables;

namespace App.modules.list;

[Action("last")]
public partial class Last : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.@this.Ok(list[list.Count - 1]));

        return Task.FromResult(Data.@this.Ok());
    }
}
