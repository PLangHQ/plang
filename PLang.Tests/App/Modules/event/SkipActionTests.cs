using App.Engine.Context;
using App.Engine.Variables;
using App.modules.@event;
using PLangEngine = App.Engine.@this;

namespace PLang.Tests.App.Modules.EventTests;

public class SkipActionTests
{
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new PLangEngine("/app");
    }

    [After(Test)]
    public void Cleanup()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Test]
    public async Task SkipAction_SetsEventOverride()
    {
        var context = _engine.Context;

        var action = new SkipAction { Context = context, Value = "override-value" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That(context.EventOverride!.Value).IsEqualTo("override-value");
    }

    [Test]
    public async Task SkipAction_NullValue_SetsOverrideWithNull()
    {
        var context = _engine.Context;

        var action = new SkipAction { Context = context, Value = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That(context.EventOverride!.Value).IsNull();
    }

    [Test]
    public async Task SkipAction_ReturnsValue()
    {
        var context = _engine.Context;

        var action = new SkipAction { Context = context, Value = 42 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task SkipAction_ObjectValue_SetsOverride()
    {
        var context = _engine.Context;
        var obj = new Dictionary<string, object> { ["status"] = 200 };

        var action = new SkipAction { Context = context, Value = obj };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.EventOverride!.Value).IsEqualTo(obj);
    }
}
