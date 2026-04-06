using App.Variables;

namespace App.modules.list;

[Action("contains")]
public partial class Contains : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(App.Data.@this.Ok(list.Contains(Value)));
        if (existing is System.Collections.IDictionary dict && Value is string key)
            return Task.FromResult(App.Data.@this.Ok(dict.Contains(key)));

        return Task.FromResult(App.Data.@this.Ok(false));
    }
}
