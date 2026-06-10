// Test action handlers used by the typed-return tests. Live in
// app.module.typedreturns.* so the source generator picks them up and
// generates SetAction/ExecuteAsync the same way production handlers get.
// Not auto-discovered (Modules.Discover walks PLang.dll only); tests
// register via app.Module.RegisterType before invoking validate.

namespace app.module.typedreturns;

/// <summary>A no-Build handler — the IClass default impl applies (Data.Ok()).</summary>
[global::app.module.Action("noopbuild")]
public partial class NoopBuild : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
}

/// <summary>Build() returns Ok("foo") — exercises the terminal-type stamping path.</summary>
[global::app.module.Action("buildreturnstype")]
public partial class BuildReturnsType : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
    public Task<global::app.data.@this> Build() => Task.FromResult(global::app.data.@this.Ok("foo"));
}

/// <summary>Build() returns Fail — exercises the abort-validation path.</summary>
[global::app.module.Action("buildfails")]
public partial class BuildFails : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
    public Task<global::app.data.@this> Build() => Task.FromResult(
        global::app.data.@this.FromError(new global::app.error.ActionError("forced build failure", "BuildFail", 400)));
}

/// <summary>Build() returns bare Ok() — exercises the "no value, no stamp" path.</summary>
[global::app.module.Action("buildbareok")]
public partial class BuildBareOk : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
    public Task<global::app.data.@this> Build() => Task.FromResult(global::app.data.@this.Ok());
}

/// <summary>Records the order Build() is invoked across an action sequence.</summary>
[global::app.module.Action("buildordered")]
public partial class BuildOrdered : global::app.module.IContext
{
    public static readonly List<string> InvocationLog = new();
    public partial global::app.data.@this<global::app.type.text.@this>? Marker { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
    public Task<global::app.data.@this> Build()
    {
        InvocationLog.Add((Marker.Materialize()?.ToString()) ?? "?");
        return Task.FromResult(global::app.data.@this.Ok());
    }
}
