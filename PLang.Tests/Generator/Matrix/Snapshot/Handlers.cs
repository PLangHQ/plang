namespace app.modules.matrix.snapshot;

[global::app.modules.Action("snapshotonerror")]
public partial class SnapshotOnError : global::app.modules.IContext
{
    public partial global::app.Data.@this<string> First { get; init; }
    public partial global::app.Data.@this<int> Second { get; init; }

    // Touch First (so backing field is set), then fail — snapshot should record both PrValue and FinalValue.
    public Task<global::app.Data.@this> Run()
    {
        var _ = First.Value; // accessed
        return Task.FromResult(global::app.Data.@this.FromError(
            new global::app.Errors.ServiceError("forced failure", "TestError", 500)));
    }
}
