using PLang.Tests.App.Fixtures;
using app.module.matrix.nullables;

namespace PLang.Tests.Generator.Matrix.Nullable;

public class StringNullableTests
{
    [Test]
    public async Task StringNullable_Missing_ReadsAsNullData()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringNullable>(app);
        await Assert.That(result.Data.Success).IsTrue();
        await Assert.That(result.Data.Value).IsNull();
    }

    [Test]
    public async Task StringNullable_Present_ResolvesToValue()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringNullable>(app,
            parameters: new[] { ("tag", (object?)"hello") });
        var typed = result.Data as global::app.data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task StringNullable_PresentWithNullValue_ReadsAsNullInitialized()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringNullable>(app,
            parameters: new[] { ("tag", (object?)null) });
        await Assert.That(result.Data.Value).IsNull();
    }
}

public class IntNullableTests
{
    [Test]
    public async Task IntNullable_Missing_ReadsAsNull()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IntNullable>(app);
        await Assert.That(result.Data.Value).IsNull();
    }

    [Test]
    public async Task IntNullable_Present_ResolvesToInt()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IntNullable>(app,
            parameters: new[] { ("maybe", (object?)42) });
        var typed = result.Data as global::app.data.@this<int>;
        await Assert.That(typed!.Value).IsEqualTo(42);
    }
}
