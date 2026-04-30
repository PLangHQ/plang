namespace App.modules.matrix.dataplain;

[global::App.modules.Action("dataplain")]
public partial class DataPlain : global::App.modules.IContext
{
    // Plain Data.@this — equivalent to Data<object>; flow-through with no unwrapping.
    public partial global::App.Data.@this Payload { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult(Payload);
}
