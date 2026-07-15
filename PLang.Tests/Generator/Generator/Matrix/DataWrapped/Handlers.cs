namespace app.module.matrix.datawrapped;

[global::app.module.Action("datawrappedstring")]
public partial class DataWrappedString : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Body { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Body);
}

[global::app.module.Action("datawrappedlist")]
public partial class DataWrappedList : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.list.@this<global::app.module.action.llm.LlmMessage>> Messages { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Messages);
}

[global::app.module.Action("datawrappeddict")]
public partial class DataWrappedDict : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.dict.@this> Headers { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Headers);
}

[global::app.module.Action("datawrappedactionlist")]
public partial class DataWrappedActionList : global::app.module.IContext
{
    // Verifies that As<T> does NOT walk into Action.@this — sub-actions retain raw %var%
    // for nested resolution at their own dispatch time.
    public partial global::app.data.@this<global::app.type.item.list.@this<global::app.type.clr.@this<global::app.goal.steps.step.actions.action.@this>>> Actions { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Actions);
}

// Cycle / depth-trip contract handler: reads .Value rather than returning the Data wrapper
// directly. The pass-through `Run() => Body` pattern in DataWrappedString happens to surface
// FromError because the FromError-Data IS the result. This handler instead consumes .Value
// and produces a derived result — exercises the post-Run __resolutionError check that surfaces
// resolution failures captured during property access.
[global::app.module.Action("datawrappedstringuses")]
public partial class DataWrappedStringUses : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Body { get; init; }
    public async Task<global::app.data.@this> Run()
    {
        // Consume through the value door — lazy resolution happens here, not at Peek.
        var len = ((await Body.Value())?.ToString())?.Length ?? 0;
        return Context.Ok(len);
    }
}
