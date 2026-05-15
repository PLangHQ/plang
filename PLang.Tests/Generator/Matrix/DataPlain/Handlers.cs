namespace app.modules.matrix.dataplain;

[global::app.modules.action("dataplain")]
public partial class DataPlain : global::app.modules.IContext
{
    // Plain Data.@this — equivalent to Data<object>; flow-through with no unwrapping.
    public partial global::app.data.@this Payload { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(Payload);
}
