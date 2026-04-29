namespace App.modules.list;

[System.ComponentModel.Description("Return the first item of the list, or empty Data if the list is empty")]
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
