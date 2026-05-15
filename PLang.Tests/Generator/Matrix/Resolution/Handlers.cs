namespace app.modules.matrix.resolution;

[global::app.modules.Action("fullvarmatch")]
public partial class FullVarMatch : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Path { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Path);
}

[global::app.modules.Action("interpolation")]
public partial class Interpolation : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Greeting { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Greeting);
}

[global::app.modules.Action("deepresolutionlist")]
public partial class DeepResolutionList : global::app.modules.IContext
{
    public partial global::app.data.@this<List<global::app.modules.llm.LlmMessage>> Messages { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Messages);
}

[global::app.modules.Action("deepresolutiondict")]
public partial class DeepResolutionDict : global::app.modules.IContext
{
    public partial global::app.data.@this<Dictionary<string, object?>> Dict { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Dict);
}

[global::app.modules.Action("reresolveacrosscalls")]
public partial class ReResolveAcrossCalls : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Value { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Value);
}

[global::app.modules.Action("concurrenthandlers")]
public partial class ConcurrentHandlers : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Value { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Value);
}
