using App.Variables;

namespace App.modules.list;

[Action("contains")]
public partial class Contains : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName).Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(Data(list.Contains(Value.Value)));
        if (existing is System.Collections.IDictionary dict && Value.Value is string key)
            return Task.FromResult(Data(dict.Contains(key)));

        return Task.FromResult(Data(false));
    }
}
