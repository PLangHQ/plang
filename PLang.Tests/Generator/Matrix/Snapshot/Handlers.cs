namespace app.modules.matrix.snapshot;

[global::app.modules.Action("snapshotonerror")]
public partial class SnapshotOnError : global::app.modules.IContext
{
    public partial global::app.data.@this<string> First { get; init; }
    public partial global::app.data.@this<int> Second { get; init; }

    // Touch First (so backing field is set), then fail — snapshot should record both PrValue and FinalValue.
    public Task<global::app.data.@this> Run()
    {
        var _ = First.Value; // accessed
        return Task.FromResult(global::app.data.@this.FromError(
            new global::app.error.ServiceError("forced failure", "TestError", 500)));
    }
}
