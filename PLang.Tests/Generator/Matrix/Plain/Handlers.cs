// Matrix handler stubs for v4 Phase 0.
// Live in App.modules.matrix.* namespace so the generator emits valid type references
// without the namespace collision with PLang.Tests.App.*. These are not auto-registered
// by Modules.Discover (which walks PLang.dll only); tests register them explicitly.

namespace App.modules.matrix.plain;

[global::App.modules.Action("stringplain")]
public partial class StringPlain : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Path { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Path);
}

[global::App.modules.Action("intplain")]
public partial class IntPlain : global::App.modules.IContext
{
    public partial global::App.Data.@this<int> Count { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Count);
}

[global::App.modules.Action("boolplain")]
public partial class BoolPlain : global::App.modules.IContext
{
    public partial global::App.Data.@this<bool> Flag { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Flag);
}

[global::App.modules.Action("pathplain")]
public partial class PathPlain : global::App.modules.IContext
{
    public partial global::App.Data.@this<global::App.FileSystem.Path> File { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(File);
}
