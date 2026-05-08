using PLang.Tests.App.Fixtures;
using App.modules.matrix.plain;

namespace PLang.Tests.Generator.Matrix.Plain;

// Matrix entries for "plain" (non-nullable, no default) Data<T> properties.

public class StringPlainTests
{
    [Test]
    public async Task StringPlain_LiteralValue_ResolvesToTypedData()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"hello") });
        var typed = result.Data as global::App.Data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task StringPlain_ReadTwice_ReturnsCachedBackingField()
    {
        // Direct ExecuteAsync — read the property twice through the handler instance.
        await using var app = new global::App.@this("/app");
        var handler = new StringPlain();
        var action = new PrAction
        {
            Module = "matrix.plain",
            ActionName = "stringplain",
            Parameters = new List<Data> { new Data("path", "hello") }
        };

        // Touch property twice via Run() — Run returns Path which means the get fired.
        // Since Run is invoked through ExecuteAsync once, we assert reference equality
        // by inspecting the same handler's field via reflection.
        var result1 = await handler.ExecuteAsync(action, app.User.Context);
        // Second read of the same property should return same instance — direct property access:
        var first = handler.Path;
        var second = handler.Path;
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task StringPlain_EmptyString_PreservedAsEmpty()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"") });
        var typed = result.Data as global::App.Data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("");
    }
}

public class IntPlainTests
{
    [Test]
    public async Task IntPlain_StringValue_ConvertsToInt()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)"42") });
        var typed = result.Data as global::App.Data.@this<int>;
        await Assert.That(typed!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task IntPlain_IntValue_FastPath()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)42) });
        var typed = result.Data as global::App.Data.@this<int>;
        await Assert.That(typed!.Value).IsEqualTo(42);
    }

    [Test]
    public async Task IntPlain_UnconvertibleString_SurfacesFromError()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<IntPlain>(app,
            parameters: new[] { ("count", (object?)"not-a-number") });
        // Conversion failure surfaces as Data.FromError with non-null Error.
        await Assert.That(result.Data.Success).IsFalse();
    }
}

public class BoolPlainTests
{
    [Test]
    public async Task BoolPlain_StringTrue_ConvertsToBool()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<BoolPlain>(app,
            parameters: new[] { ("flag", (object?)"true") });
        var typed = result.Data as global::App.Data.@this<bool>;
        await Assert.That(typed!.Value).IsTrue();
    }

    [Test]
    public async Task BoolPlain_BoolValue_FastPath()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<BoolPlain>(app,
            parameters: new[] { ("flag", (object?)true) });
        var typed = result.Data as global::App.Data.@this<bool>;
        await Assert.That(typed!.Value).IsTrue();
    }
}

public class PathPlainTests
{
    [Test]
    public async Task PathPlain_StringValue_UsesStaticResolve()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<PathPlain>(app,
            parameters: new[] { ("file", (object?)"data/x.txt") });
        var typed = result.Data as global::App.Data.@this<global::App.FileSystem.Path>;
        await Assert.That(typed!.Value).IsNotNull();
        await Assert.That(typed.Value).IsTypeOf<global::App.FileSystem.Path>();
    }

    [Test]
    public async Task PathPlain_PathValue_FastPath()
    {
        await using var app = new global::App.@this("/app");
        var path = new global::App.FileSystem.Path("/already-a-path.txt");
        var result = await MatrixRunner.RunAsync<PathPlain>(app,
            parameters: new[] { ("file", (object?)path) });
        var typed = result.Data as global::App.Data.@this<global::App.FileSystem.Path>;
        await Assert.That(ReferenceEquals(typed!.Value, path)).IsTrue();
    }
}
