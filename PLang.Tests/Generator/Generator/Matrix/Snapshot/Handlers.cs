namespace app.module.matrix.snapshot;

[global::app.module.Action("snapshotonerror")]
public partial class SnapshotOnError : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this> First { get; init; }
    public partial global::app.data.@this<global::app.type.number.@this> Second { get; init; }

    // Touch First (so backing field is set), then fail — snapshot should record both PrValue and FinalValue.
    public Task<global::app.data.@this> Run()
    {
        var _ = (First.Peek()); // accessed
        return Task.FromResult(global::app.data.@this.FromError(
            new global::app.error.ServiceError("forced failure", "TestError", 500)));
    }
}
