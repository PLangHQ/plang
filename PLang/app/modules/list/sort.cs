using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Sort the list in place ascending by default, or descending when Descending is true")]
[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    [Default(false)]
    public partial data.@this<bool> Descending { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (Descending.Value)
            list.Sort((a, b) => Comparer<object>.Default.Compare(b, a));
        else
            list.Sort((a, b) => Comparer<object>.Default.Compare(a, b));

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
