using PLang.Tests.App.Fixtures;
using app.module.matrix.dataplain;

namespace PLang.Tests.Generator.Matrix.DataPlain;

public class DataPlainTests
{
    [Test]
    public async Task DataPlain_StringValue_PassesThrough()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)"hello") });
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task DataPlain_IntValue_PassesThrough()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)42) });
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task DataPlain_ListValue_PassesThrough()
    {
        await using var app = TestApp.Create("/app");
        var raw = new List<object?> { 1, 2, 3 };
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)raw) });
        // A list value rides as the native list type now.
        await Assert.That((await result.Data.Value())).IsTypeOf<global::app.type.item.list.@this>();
        var list = global::app.type.item.@this.Lower<List<object?>>(await result.Data.Value());
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    [Test]
    public async Task DataPlain_DictValue_PassesThrough()
    {
        await using var app = TestApp.Create("/app");
        var raw = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)raw) });
        // A dict value rides as the native dict type now.
        await Assert.That((await result.Data.Value())).IsTypeOf<global::app.type.item.dict.@this>();
    }

    [Test]
    public async Task DataPlain_VarReference_ResolvesAsObject()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.dataplain.DataPlain>(app,
            parameters: new[] { ("payload", (object?)"%name%") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("world");
    }
}
