using App.Variables;

namespace App.modules.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(App.Data.@this.Ok(list.IndexOf(Value)));

        return Task.FromResult(App.Data.@this.Ok(-1));
    }
}
