using app.variable;

namespace app.module.list;

[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = await data.GetChild("Count");

        // The typed surface answers in a `number`; raw int covers IList infra.
        int count = (countData.IsInitialized ? await countData.Value() : null) switch
        {
            global::app.type.number.@this n => n.ToInt32(),
            int i => i,
            _ => 0,
        };
        if (count > 0)
        {
            var last = await data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return global::app.data.@this.Ok((await last.Value()));
        }

        return global::app.data.@this.Ok();
    }
}
