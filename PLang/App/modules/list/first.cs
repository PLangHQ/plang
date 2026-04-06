using App.Engine.Variables;

namespace App.modules.list;

[Action("first")]
public partial class First : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.Ok(list[0]));

        return Task.FromResult(Data.Ok());
    }
}
