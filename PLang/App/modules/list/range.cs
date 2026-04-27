using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Generate a list of integers from Start to End inclusive, stepping by Step (default 1)")]
[Action("range")]
public partial class Range : IContext
{
    public partial Data.@this<int> Start { get; init; }
    public partial Data.@this<int> End { get; init; }
    [Default(1)]
    public partial Data.@this<int> Step { get; init; }

    public Task<Data.@this> Run()
    {
        if (Step.Value == 0)
            return Task.FromResult(Error(
                new App.Errors.ValidationError("Step cannot be zero", "InvalidStep")));

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

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
