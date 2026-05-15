namespace app.modules.matrix.isnotnull;

[global::app.modules.Action("isnotnullprop")]
public partial class IsNotNullProp : global::app.modules.IContext
{
    [global::app.modules.IsNotNull]
    public partial global::app.Data.@this<string> Required { get; init; }

    public Task<global::app.Data.@this> Run() => Task.FromResult<global::app.Data.@this>(Required);
}
