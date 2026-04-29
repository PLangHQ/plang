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
