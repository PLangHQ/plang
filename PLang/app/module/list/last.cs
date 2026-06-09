using app.variable;

namespace app.module.list;

[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && (await countData.Value()) is int count && count > 0)
        {
            var last = data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return global::app.data.@this.Ok((await last.Value()));
        }

        return global::app.data.@this.Ok();
    }
}
