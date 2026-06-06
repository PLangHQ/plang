using app.variable;

namespace app.module.variable;

[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<app.variable.@this> Name { get; init; }

    public Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        return Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.Ok(Context.Variable.Contains(Name.Value)));
    }
}
