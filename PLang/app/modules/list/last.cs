using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Return the last item of the list, or empty Data if the list is empty")]
[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<object>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int count && count > 0)
        {
            var last = data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return Task.FromResult(global::app.data.@this<object>.Ok(last.Value));
        }

        return Task.FromResult(new global::app.data.@this<object>());
    }
}
