using PLang.Tests.App.Fixtures;
using app.modules.matrix.withdefault;

namespace PLang.Tests.Generator.Matrix.WithDefault;

public class StringWithDefaultTests
{
    [Test]
    public async Task StringWithDefault_Missing_UsesDefault()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringWithDefault>(app);
        var typed = result.Data as global::app.data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task StringWithDefault_Present_OverridesDefault()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringWithDefault>(app,
            parameters: new[] { ("greeting", (object?)"world") });
        var typed = result.Data as global::app.data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("world");
    }
}

public class IntWithDefaultTests
{
    [Test]
    public async Task IntWithDefault_Missing_Returns42()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IntWithDefault>(app);
        var typed = result.Data as global::app.data.@this<int>;
        await Assert.That(typed!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task IntWithDefault_Present_OverridesAndConverts()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<IntWithDefault>(app,
            parameters: new[] { ("count", (object?)"7") });
        var typed = result.Data as global::app.data.@this<int>;
        await Assert.That(typed!.Value).IsEqualTo(7);
    }
}

public class EnumWithDefaultTests
{
    [Test]
    public async Task EnumWithDefault_Missing_ReturnsDefaultMember()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<EnumWithDefault>(app);
        var typed = result.Data as global::app.data.@this<MatrixEnum>;
        await Assert.That(typed!.Value).IsEqualTo(MatrixEnum.A);
    }

    [Test]
    public async Task EnumWithDefault_StringValue_ConvertsToMember()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<EnumWithDefault>(app,
            parameters: new[] { ("choice", (object?)"B") });
        var typed = result.Data as global::app.data.@this<MatrixEnum>;
        await Assert.That(typed!.Value).IsEqualTo(MatrixEnum.B);
    }
}

public class BoolWithDefaultTests
{
    [Test]
    public async Task BoolWithDefault_Missing_ReturnsFalse()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<BoolWithDefault>(app);
        var typed = result.Data as global::app.data.@this<bool>;
        await Assert.That(typed!.Value).IsFalse();
    }

    [Test]
    public async Task BoolWithDefault_StringTrue_OverridesDefault()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<BoolWithDefault>(app,
            parameters: new[] { ("flag", (object?)"true") });
        var typed = result.Data as global::app.data.@this<bool>;
        await Assert.That(typed!.Value).IsTrue();
    }
}
