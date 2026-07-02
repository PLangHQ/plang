using app;
using app.actor.context;
using app.variable;
using app.module.condition;
using app.type.path;

namespace PLang.Tests.App.Modules.condition;

public class CompareHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public CompareHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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
        var action = new Compare { Context = _app.User.Context, Left = _app.User.Context.Ok(10), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsEqualTo(true);
    }

    [Test]
    public async Task Run_GreaterThan_Fails_ReturnsDataWithFalse()
    {
        var action = new Compare { Context = _app.User.Context, Left = _app.User.Context.Ok(3), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsEqualTo(false);
    }

    [Test]
    public async Task Run_ResultValueIsBool()
    {
        var action = new Compare { Context = _app.User.Context, Left = _app.User.Context.Ok(5), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator("==")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await Assert.That((await result.Value()) is global::app.type.@bool.@this).IsTrue();
        await Assert.That((await result.Value())!.Value).IsTrue();
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
        var action = new Compare { Context = _app.User.Context, Left = _app.User.Context.Ok(new object()), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("cannot order");
    }
}
