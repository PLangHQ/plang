using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Generate a list of integers from Start to End inclusive, stepping by Step (default 1)")]
[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<int> Start { get; init; }
    public partial data.@this<int> End { get; init; }
    [Default(1)]
    public partial data.@this<int> Step { get; init; }

    public Task<data.@this> Run()
    {
        if (Step.Value == 0)
            return Task.FromResult(Error(
                new app.errors.ValidationError("Step cannot be zero", "InvalidStep")));

        var list = new List<object?>();
        if (Step.Value > 0)
        {
            for (int i = Start.Value; i <= End.Value; i += Step.Value)
                list.Add(i);
        }
        else
        {
            for (int i = Start.Value; i >= End.Value; i += Step.Value)
                list.Add(i);
        }

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
