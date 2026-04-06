using App;
using App.Context;
using App.Variables;
using App.modules.condition;
using App.SafeFileSystem;

namespace PLang.Tests.App.Modules.condition;

public class CompareHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly App.@this _engine;

    public CompareHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new App.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Run_GreaterThan_ReturnsDataWithTrue()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = Data.Ok(10), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_GreaterThan_Fails_ReturnsDataWithFalse()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = Data.Ok(3), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_ResultValueIsBool()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = Data.Ok(5), Operator = "==", Right = Data.Ok(5) };
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
        var action = new Compare { Context = _engine.CreateContext(), Left = Data.Ok(new object()), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("does not support comparison");
    }
}
