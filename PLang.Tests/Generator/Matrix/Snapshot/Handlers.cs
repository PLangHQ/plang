namespace App.modules.matrix.snapshot;

[global::App.modules.Action("snapshotonerror")]
public partial class SnapshotOnError : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> First { get; init; }
    public partial global::App.Data.@this<int> Second { get; init; }

    // Touch First (so backing field is set), then fail — snapshot should record both PrValue and FinalValue.
    public Task<global::App.Data.@this> Run()
    {
        var _ = First.Value; // accessed
        return Task.FromResult(global::App.Data.@this.FromError(
            new global::App.Errors.ServiceError("forced failure", "TestError", 500)));
    }
}
