namespace App.modules.matrix.resolution;

[global::App.modules.Action("fullvarmatch")]
public partial class FullVarMatch : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Path { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Path);
}

[global::App.modules.Action("interpolation")]
public partial class Interpolation : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Greeting { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Greeting);
}

[global::App.modules.Action("deepresolutionlist")]
public partial class DeepResolutionList : global::App.modules.IContext
{
    public partial global::App.Data.@this<List<global::App.modules.llm.LlmMessage>> Messages { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Messages);
}

[global::App.modules.Action("deepresolutiondict")]
public partial class DeepResolutionDict : global::App.modules.IContext
{
    public partial global::App.Data.@this<Dictionary<string, object?>> Dict { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Dict);
}

[global::App.modules.Action("reresolveacrosscalls")]
public partial class ReResolveAcrossCalls : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Value { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Value);
}

[global::App.modules.Action("concurrenthandlers")]
public partial class ConcurrentHandlers : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Value { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Value);
}
