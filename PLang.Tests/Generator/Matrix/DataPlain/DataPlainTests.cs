using PLang.Tests.App.Fixtures;
using App.modules.matrix.dataplain;

namespace PLang.Tests.Generator.Matrix.DataPlain;

public class DataPlainTests
{
    [Test]
    public async Task DataPlain_StringValue_PassesThrough()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<global::App.modules.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)"hello") });
        await Assert.That(result.Data.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task DataPlain_IntValue_PassesThrough()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<global::App.modules.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)42) });
        await Assert.That(result.Data.Value).IsEqualTo(42);
    }

    [Test]
    public async Task DataPlain_ListValue_PassesThrough()
    {
        await using var app = new global::App.@this("/app");
        var raw = new List<object?> { 1, 2, 3 };
        var result = await MatrixRunner.RunAsync<global::App.modules.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)raw) });
        await Assert.That(result.Data.Value).IsTypeOf<List<object?>>();
        var list = result.Data.Value as List<object?>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    [Test]
    public async Task DataPlain_DictValue_PassesThrough()
    {
        await using var app = new global::App.@this("/app");
        var raw = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
        var result = await MatrixRunner.RunAsync<global::App.modules.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)raw) });
        await Assert.That(result.Data.Value).IsTypeOf<Dictionary<string, object?>>();
    }

    [Test]
    public async Task DataPlain_VarReference_ResolvesAsObject()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<global::App.modules.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)"%name%") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });
        await Assert.That(result.Data.Value).IsEqualTo("world");
    }
}
