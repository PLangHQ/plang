namespace app.module.matrix.isnotnull;

[global::app.module.Action("isnotnullprop")]
public partial class IsNotNullProp : global::app.module.IContext
{
    [global::app.module.IsNotNull]
    public partial global::app.data.@this<string> Required { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Required);
}
