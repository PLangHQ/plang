using app.actor.context;
using app.type.item.variable;
using app.module.action.@event;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.EventTests;

// event.skipAction answers its value marked Handled: returned from a before-action handler, that answer cancels
// the action and is its result.
public class SkipActionTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public void Cleanup()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Test]
    public async Task SkipAction_AnswersItsValue_MarkedHandled()
    {
        var context = _app.User.Context;

        var action = new SkipAction(context) { Value = new global::app.data.@this("", "override-value", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That(result.Handled).IsTrue();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("override-value");
    }

    [Test]
    public async Task SkipAction_NullValue_AnswersEmpty_MarkedHandled()
    {
        var context = _app.User.Context;

        var action = new SkipAction(context) { Value = null };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That(result.Handled).IsTrue();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task SkipAction_ReturnsValue()
    {
        var context = _app.User.Context;

        var action = new SkipAction(context) { Value = new global::app.data.@this("", 42, context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task SkipAction_ObjectValue_IsTheAnswer()
    {
        var context = _app.User.Context;
        var obj = new Dictionary<string, object> { ["status"] = 200 };

        var action = new SkipAction(context) { Value = new global::app.data.@this("", obj, context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That(Lower<object>(await result.Value())).IsEqualTo(obj);
    }
}
