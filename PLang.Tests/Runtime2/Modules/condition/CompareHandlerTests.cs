using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Modules.condition;

public class CompareHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public CompareHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(_tempDir, fileSystem: _fs);
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
        var action = new Compare { Context = _engine.CreateContext(), Left = 10, Operator = ">", Right = 5 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_GreaterThan_Fails_ReturnsDataWithFalse()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = 3, Operator = ">", Right = 5 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_ResultValueIsBool()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = 5, Operator = "==", Right = 5 };
        var result = await action.Run();

        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Run_UnsupportedOperator_ReturnsEvaluationError()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = 1, Operator = "xor", Right = 2 };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("xor");
        await Assert.That(result.Error!.Message).Contains("Int32");
    }

    [Test]
    public async Task Run_DoesNotSetConditionSignal()
    {
        var context = _engine.CreateContext();
        var action = new Compare { Context = context, Left = 10, Operator = ">", Right = 5 };
        await action.Run();

        var signal = context.MemoryStack.Get("__condition__");
        await Assert.That(signal).IsNull();
    }

    [Test]
    public async Task Run_NonComparableType_ReturnsEvaluationError()
    {
        var action = new Compare { Context = _engine.CreateContext(), Left = new object(), Operator = ">", Right = 5 };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("does not support comparison");
    }
}
