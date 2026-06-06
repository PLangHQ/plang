// Matrix handler stubs for v4 Phase 0.
// Live in App.module.matrix.* namespace so the generator emits valid type references
// without the namespace collision with PLang.Tests.App.*. These are not auto-registered
// by Modules.Discover (which walks PLang.dll only); tests register them explicitly.

namespace app.module.matrix.plain;

[global::app.module.Action("stringplain")]
public partial class StringPlain : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.text.@this> Path { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Path);
}

[global::app.module.Action("intplain")]
public partial class IntPlain : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.number.@this> Count { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Count);
}

[global::app.module.Action("boolplain")]
public partial class BoolPlain : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.@bool.@this> Flag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Flag);
}

[global::app.module.Action("pathplain")]
public partial class PathPlain : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.path.@this> File { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(File);
}
