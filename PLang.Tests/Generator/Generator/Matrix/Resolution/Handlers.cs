namespace app.module.matrix.resolution;

[global::app.module.Action("fullvarmatch")]
public partial class FullVarMatch : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Path { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Path);
}

[global::app.module.Action("interpolation")]
public partial class Interpolation : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Greeting { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Greeting);
}

[global::app.module.Action("deepresolutionlist")]
public partial class DeepResolutionList : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.list.@this<global::app.module.llm.LlmMessage>> Messages { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Messages);
}

[global::app.module.Action("deepresolutiondict")]
public partial class DeepResolutionDict : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.dict.@this> Dict { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Dict);
}

[global::app.module.Action("reresolveacrosscalls")]
public partial class ReResolveAcrossCalls : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Value { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Value);
}

[global::app.module.Action("concurrenthandlers")]
public partial class ConcurrentHandlers : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Value { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Value);
}
