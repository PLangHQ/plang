namespace app.modules.matrix.datawrapped;

[global::app.modules.Action("datawrappedstring")]
public partial class DataWrappedString : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Body { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Body);
}

[global::app.modules.Action("datawrappedlist")]
public partial class DataWrappedList : global::app.modules.IContext
{
    public partial global::app.data.@this<List<global::app.modules.llm.LlmMessage>> Messages { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Messages);
}

[global::app.modules.Action("datawrappeddict")]
public partial class DataWrappedDict : global::app.modules.IContext
{
    public partial global::app.data.@this<Dictionary<string, object?>> Headers { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Headers);
}

[global::app.modules.Action("datawrappedactionlist")]
public partial class DataWrappedActionList : global::app.modules.IContext
{
    // Verifies that As<T> does NOT walk into Action.@this — sub-actions retain raw %var%
    // for nested resolution at their own dispatch time.
    public partial global::app.data.@this<List<global::app.Goals.Goal.Steps.Step.Actions.Action.@this>> Actions { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Actions);
}

// Cycle / depth-trip contract handler: reads .Value rather than returning the Data wrapper
// directly. The pass-through `Run() => Body` pattern in DataWrappedString happens to surface
// FromError because the FromError-Data IS the result. This handler instead consumes .Value
// and produces a derived result — exercises the post-Run __resolutionError check that surfaces
// resolution failures captured during property access.
[global::app.modules.Action("datawrappedstringuses")]
public partial class DataWrappedStringUses : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Body { get; init; }
    public Task<global::app.data.@this> Run()
    {
        var len = Body.Value?.Length ?? 0;
        return Task.FromResult(global::app.data.@this.Ok(len));
    }
}
