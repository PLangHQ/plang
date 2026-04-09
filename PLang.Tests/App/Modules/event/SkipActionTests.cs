using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules.@event;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.EventTests;

public class SkipActionTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new PLangEngine("/app");
    }

    [After(Test)]
    public void Cleanup()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Test]
    public async Task SkipAction_SetsEventOverride()
    {
        var context = _app.Context;

        var action = new SkipAction { Context = context, Value = "override-value" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That(context.EventOverride!.Value).IsEqualTo("override-value");
    }

    [Test]
    public async Task SkipAction_NullValue_SetsOverrideWithNull()
    {
        var context = _app.Context;

        var action = new SkipAction { Context = context, Value = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That(context.EventOverride!.Value).IsNull();
    }

    [Test]
    public async Task SkipAction_ReturnsValue()
    {
        var context = _app.Context;

        var action = new SkipAction { Context = context, Value = 42 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task SkipAction_ObjectValue_SetsOverride()
    {
        var context = _app.Context;
        var obj = new Dictionary<string, object> { ["status"] = 200 };

        var action = new SkipAction { Context = context, Value = obj };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride!.Value).IsEqualTo(obj);
    }
}
