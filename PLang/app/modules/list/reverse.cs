using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Reverse the order of items in the list in place")]
[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        list.Reverse();
        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, app.Data.Type.FromName("list")));
    }
}
