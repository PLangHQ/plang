using app;
using app.actor.context;
using app.variables;
using app.modules.condition;
using app.types.path;
using app.types.path.Default;

namespace PLang.Tests.App.Modules.condition;

public class CompareHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::app.@this _app;

    public CompareHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _app = new global::app.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Run_GreaterThan_ReturnsDataWithTrue()
    {
        var action = new Compare { Context = _app.User.Context, Left = Data.Ok(10), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_GreaterThan_Fails_ReturnsDataWithFalse()
    {
        var action = new Compare { Context = _app.User.Context, Left = Data.Ok(3), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_ResultValueIsBool()
    {
        var action = new Compare { Context = _app.User.Context, Left = Data.Ok(5), Operator = new Operator("=="), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Run_UnsupportedOperator_ThrowsOnConstruction()
    {
        await Assert.That(() => new Operator("xor")).ThrowsException()
            .WithMessageMatching("*Unsupported operator*");
    }

    [Test]
    public async Task Run_NonComparableType_ReturnsEvaluationError()
    {
        var action = new Compare { Context = _app.User.Context, Left = Data.Ok(new object()), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("does not support comparison");
    }
}
