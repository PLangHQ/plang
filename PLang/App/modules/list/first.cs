namespace App.modules.list;

[Action("first")]
public partial class First : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var first = data.GetChild("[0]");

        return Task.FromResult(first.IsInitialized ? Data(first.Value) : Data());
    }
}
