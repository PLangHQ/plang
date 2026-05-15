namespace app.modules.matrix.dataplain;

[global::app.modules.Action("dataplain")]
public partial class DataPlain : global::app.modules.IContext
{
    // Plain Data.@this — equivalent to Data<object>; flow-through with no unwrapping.
    public partial global::app.Data.@this Payload { get; init; }
    public Task<global::app.Data.@this> Run() => Task.FromResult(Payload);
}
