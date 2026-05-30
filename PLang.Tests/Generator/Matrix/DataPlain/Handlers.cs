namespace app.module.matrix.dataplain;

[global::app.module.Action("dataplain")]
public partial class DataPlain : global::app.module.IContext
{
    // Plain Data.@this — equivalent to Data<object>; flow-through with no unwrapping.
    public partial global::app.data.@this Payload { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(Payload);
}
