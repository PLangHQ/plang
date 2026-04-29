namespace App.modules.matrix.isnotnull;

[global::App.modules.Action("isnotnullprop")]
public partial class IsNotNullProp : global::App.modules.IContext
{
    [global::App.modules.IsNotNull]
    public partial global::App.Data.@this<string> Required { get; init; }

    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Required);
}
