namespace App.modules.matrix.datawrapped;

[global::App.modules.Action("datawrappedstring")]
public partial class DataWrappedString : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Body { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Body);
}

[global::App.modules.Action("datawrappedlist")]
public partial class DataWrappedList : global::App.modules.IContext
{
    public partial global::App.Data.@this<List<global::App.modules.llm.LlmMessage>> Messages { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Messages);
}

[global::App.modules.Action("datawrappeddict")]
public partial class DataWrappedDict : global::App.modules.IContext
{
    public partial global::App.Data.@this<Dictionary<string, object?>> Headers { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Headers);
}

[global::App.modules.Action("datawrappedactionlist")]
public partial class DataWrappedActionList : global::App.modules.IContext
{
    // Verifies that As<T> does NOT walk into Action.@this — sub-actions retain raw %var%
    // for nested resolution at their own dispatch time.
    public partial global::App.Data.@this<List<global::App.Goals.Goal.Steps.Step.Actions.Action.@this>> Actions { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Actions);
}

// Cycle / depth-trip contract handler: reads .Value rather than returning the Data wrapper
// directly. The pass-through `Run() => Body` pattern in DataWrappedString happens to surface
// FromError because the FromError-Data IS the result. This handler instead consumes .Value
// and produces a derived result — exercises the post-Run __resolutionError check that surfaces
// resolution failures captured during property access.
[global::App.modules.Action("datawrappedstringuses")]
public partial class DataWrappedStringUses : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Body { get; init; }
    public Task<global::App.Data.@this> Run()
    {
        var len = Body.Value?.Length ?? 0;
        return Task.FromResult(global::App.Data.@this.Ok(len));
    }
}
