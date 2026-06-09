using app.module.goal;

namespace PLang.Tests.App.Modules.goal;

public class GoalReturnTests
{
    private (global::app.actor.context.@this context, global::app.variable.list.@this memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Return_NullData_ReturnsOkWithReturnedFlag()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = null };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_WithData_PropagatesValue()
    {
        var (context, _) = CreateContext();
        var data = global::app.data.@this.Ok("hello");
        var action = new Return { Context = context, Data = data };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That((await result.Value())!.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Return_DepthGreaterThanOne_SetsReturnDepth()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::app.data.@this.Ok(), Depth = (global::app.type.number.@this)3 };
        var result = await action.Run();

        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.ReturnDepth).IsEqualTo(3);
    }

    [Test]
    public async Task Return_DepthZero_DefaultsToOne()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::app.data.@this.Ok(), Depth = (global::app.type.number.@this)0 };
        var result = await action.Run();

        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_NegativeDepth_DefaultsToOne()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::app.data.@this.Ok(), Depth = (global::app.type.number.@this)(-5) };
        var result = await action.Run();

        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_FailingData_PropagatesError()
    {
        var (context, _) = CreateContext();
        var error = global::app.data.@this.FromError(
            new global::app.error.Error("something broke", "TestError", 500));
        var action = new Return { Context = context, Data = error };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.Error!.Key).IsEqualTo("TestError");
    }
}
