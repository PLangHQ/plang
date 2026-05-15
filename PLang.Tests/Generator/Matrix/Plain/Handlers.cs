// Matrix handler stubs for v4 Phase 0.
// Live in App.modules.matrix.* namespace so the generator emits valid type references
// without the namespace collision with PLang.Tests.App.*. These are not auto-registered
// by Modules.Discover (which walks PLang.dll only); tests register them explicitly.

namespace app.modules.matrix.plain;

[global::app.modules.action("stringplain")]
public partial class StringPlain : global::app.modules.IContext
{
    public partial global::app.data.@this<string> Path { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Path);
}

[global::app.modules.action("intplain")]
public partial class IntPlain : global::app.modules.IContext
{
    public partial global::app.data.@this<int> Count { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Count);
}

[global::app.modules.action("boolplain")]
public partial class BoolPlain : global::app.modules.IContext
{
    public partial global::app.data.@this<bool> Flag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Flag);
}

[global::app.modules.action("pathplain")]
public partial class PathPlain : global::app.modules.IContext
{
    public partial global::app.data.@this<global::app.filesystem.path> File { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(File);
}
