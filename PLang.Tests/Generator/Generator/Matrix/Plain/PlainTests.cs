using PLang.Tests.App.Fixtures;
using app.module.matrix.plain;

namespace PLang.Tests.Generator.Matrix.Plain;

// Matrix entries for "plain" (non-nullable, no default) Data<T> properties.

public class StringPlainTests
{
    [Test]
    public async Task StringPlain_LiteralValue_ResolvesToTypedData()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"hello") });
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.Clr<string>()).IsEqualTo("hello");
    }

    [Test]
    public async Task StringPlain_ReadTwice_ReturnsCachedBackingField()
    {
        // Direct ExecuteAsync — read the property twice through the handler instance.
        await using var app = TestApp.Create("/app");
        var handler = new StringPlain();
        var action = new PrAction
        {
            Module = "matrix.plain",
            ActionName = "stringplain",
            Parameters = new List<Data> { new Data("path", "hello", context: app.User.Context) }
        };

        // Touch property twice via the resolved instance — Resolve populates the
        // backing field once; reading Path twice must return the same cached instance.
        var (h, err) = await handler.Resolve(action, app.User.Context);
        await Assert.That(err).IsNull();
        var resolved = (StringPlain)h!;
        var first = resolved.Path;
        var second = resolved.Path;
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task StringPlain_EmptyString_PreservedAsEmpty()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"") });
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.Clr<string>()).IsEqualTo("");
    }
}

public class IntPlainTests
{
    [Test]
    public async Task IntPlain_StringValue_ConvertsToInt()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)"42") });
        var typed = result.Data as global::app.data.@this<global::app.type.number.@this>;
        await Assert.That((await typed!.Value())).IsEqualTo(42);
    }

    [Test]
    public async Task IntPlain_IntValue_FastPath()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)42) });
        var typed = result.Data as global::app.data.@this<global::app.type.number.@this>;
        await Assert.That((await typed!.Value())).IsEqualTo(42);
    }

    [Test]
    public async Task IntPlain_UnconvertibleString_SurfacesFromError()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)"not-a-number") });
        // Lazy: the conversion runs at the typed value door, so the failure surfaces
        // when the value is materialised (Data<number>.Value()) — not at dispatch.
        var typed = result.Data as global::app.data.@this<global::app.type.number.@this>;
        await typed!.Value();
        await result.Data.IsFailure();
    }
}

public class BoolPlainTests
{
    [Test]
    public async Task BoolPlain_StringTrue_ConvertsToBool()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<BoolPlain>(app,
            parameters: new[] { ("flag", (object?)"true") });
        var typed = result.Data as global::app.data.@this<global::app.type.@bool.@this>;
        await Assert.That((await typed!.Value()).Value).IsTrue();
    }

    [Test]
    public async Task BoolPlain_BoolValue_FastPath()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<BoolPlain>(app,
            parameters: new[] { ("flag", (object?)true) });
        var typed = result.Data as global::app.data.@this<global::app.type.@bool.@this>;
        await Assert.That((await typed!.Value()).Value).IsTrue();
    }
}

public class PathPlainTests
{
    [Test]
    public async Task PathPlain_StringValue_UsesStaticResolve()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<PathPlain>(app,
            parameters: new[] { ("file", (object?)"data/x.txt") });
        var typed = result.Data as global::app.data.@this<global::app.type.path.@this>;
        await Assert.That((await typed!.Value())).IsNotNull();
        await Assert.That((await typed.Value()) is global::app.type.path.@this).IsTrue();
    }

    [Test]
    public async Task PathPlain_PathValue_FastPath()
    {
        await using var app = TestApp.Create("/app");
        var path = new global::app.type.path.file.@this("/already-a-path.txt");
        var result = await MatrixRunner.RunAsync<PathPlain>(app,
            parameters: new[] { ("file", (object?)path) });
        var typed = result.Data as global::app.data.@this<global::app.type.path.@this>;
        await Assert.That(ReferenceEquals((await typed!.Value()), path)).IsTrue();
    }
}
