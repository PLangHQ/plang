using App.Variables;

namespace App.modules.list;

[Action("count")]
public partial class Count : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName).Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(Data(list.Count));
        if (existing is System.Collections.IDictionary dict)
            return Task.FromResult(Data(dict.Count));

        return Task.FromResult(Data(0));
    }
}
