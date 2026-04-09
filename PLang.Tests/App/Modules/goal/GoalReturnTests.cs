using global::App.modules.goal;

namespace PLang.Tests.App.Modules.goal;

public class GoalReturnTests
{
    private (global::App.Actor.Context.@this context, global::App.Variables.@this memory) CreateContext()
    {
        var app = new global::App.@this("/app");
        return (app.Context, app.Context.Variables);
    }

    [Test]
    public async Task Return_NullData_ReturnsOkWithReturnedFlag()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_WithData_PropagatesValue()
    {
        var (context, _) = CreateContext();
        var data = global::App.Data.@this.Ok("hello");
        var action = new Return { Context = context, Data = data };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Return_DepthGreaterThanOne_SetsReturnDepth()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::App.Data.@this.Ok(), Depth = 3 };
        var result = await action.Run();

        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.ReturnDepth).IsEqualTo(3);
    }

    [Test]
    public async Task Return_DepthZero_DefaultsToOne()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::App.Data.@this.Ok(), Depth = 0 };
        var result = await action.Run();

        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_NegativeDepth_DefaultsToOne()
    {
        var (context, _) = CreateContext();
        var action = new Return { Context = context, Data = global::App.Data.@this.Ok(), Depth = -5 };
        var result = await action.Run();

        await Assert.That(result.ReturnDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Return_FailingData_PropagatesError()
    {
        var (context, _) = CreateContext();
        var error = global::App.Data.@this.FromError(
            new global::App.Errors.Error("something broke", "TestError", 500));
        var action = new Return { Context = context, Data = error };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Returned).IsTrue();
        await Assert.That(result.Error!.Key).IsEqualTo("TestError");
    }
}
