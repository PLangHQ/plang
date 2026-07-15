using app.variable;

namespace app.module.action.list;

[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = await data.Get("Count");

        // The typed surface answers in a `number`; raw int covers IList infra.
        int count = (countData.IsInitialized ? await countData.Value() : null) switch
        {
            global::app.type.item.number.@this n => n.ToInt32(),
            _ => 0,
        };
        if (count > 0)
        {
            var last = await data.Get($"[{count - 1}]");
            if (last.IsInitialized) return Context.Ok((await last.Value()));
        }

        return Context.Ok();
    }
}
