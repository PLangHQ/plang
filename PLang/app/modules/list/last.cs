using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Return the last item of the list, or empty Data if the list is empty")]
[Action("last")]
public partial class Last : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int count && count > 0)
        {
            var last = data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return Task.FromResult(Data(last.Value));
        }

        return Task.FromResult(Data());
    }
}
