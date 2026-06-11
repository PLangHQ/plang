using app.actor.context;
using app.variable;
using app.module.@event;
using PLangEngine = global::app.@this;

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
        var context = _app.User.Context;

        var action = new SkipAction { Context = context, Value = new global::app.data.@this("", "override-value")};
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That((await context.EventOverride!.Value())?.ToString()).IsEqualTo("override-value");
    }

    [Test]
    public async Task SkipAction_NullValue_SetsOverrideWithNull()
    {
        var context = _app.User.Context;

        var action = new SkipAction { Context = context, Value = null };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(context.EventOverride).IsNotNull();
        await Assert.That((await context.EventOverride!.Value())).IsNull();
    }

    [Test]
    public async Task SkipAction_ReturnsValue()
    {
        var context = _app.User.Context;

        var action = new SkipAction { Context = context, Value = new global::app.data.@this("", 42)};
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task SkipAction_ObjectValue_SetsOverride()
    {
        var context = _app.User.Context;
        var obj = new Dictionary<string, object> { ["status"] = 200 };

        var action = new SkipAction { Context = context, Value = new global::app.data.@this("", obj)};
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(global::app.type.item.@this.Lower<object>(await context.EventOverride!.Value())).IsEqualTo(obj);
    }
}
