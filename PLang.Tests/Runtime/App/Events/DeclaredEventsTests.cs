using Window = global::app.module.window.type.window.@this;

namespace PLang.Tests.App.Events;

// A plang type declares the events its items have besides the core's ([Events(…)]): a window's opened … closed, a
// screen element's click. A declared event binds and fires like a core one; a name its type doesn't declare is refused;
// a core item's events are the core's alone.
public class DeclaredEventsTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Test] public async Task AWindowsDeclaredEvent_BindsAndFires()
    {
        var window = new Window();
        global::app.data.@this? heard = null;
        _app.actor.list.User.on.Bind(window, "closed", global::app.@event.When.after,
            (_, data, _) => { heard = data; return System.Threading.Tasks.Task.FromResult(data); },
            global::app.@event.binding.Scope.actor);

        await _app.actor.list.User.on[window]["closed"]!.After(window, Ctx.Ok((global::app.type.item.text.@this)"{\"window\":\"closed\",\"id\":3}"), Ctx);

        await Assert.That(heard).IsNotNull();
        await Assert.That(_app.actor.list.User.on[window].Names).Contains("closed");
        await Assert.That(_app.actor.list.User.on[window].Names).Contains("create");
    }

    // nothing bound on a window yet: its declared events are still there to raise, shared and closed to bindings
    [Test] public async Task AnUnboundWindow_StillAnswersItsDeclaredEvents()
    {
        var window = new Window();

        await Assert.That(window.on["opened"]).IsNotNull();
        var raised = await window.on["opened"]!.After(window, Ctx.Ok(), Ctx);
        await raised.IsSuccess();
    }

    // a goal binds every window's event through the list (on.event Event=%browser.window.on.closed%, PlangOS's shell):
    // a step by name is the list's own, its events with it; a step by index is a window
    [Test] public async Task TheWindowListsEvents_AreReachedByName_AnIndexIsAWindow()
    {
        var list = new global::app.module.window.type.window.list.@this(null!);
        var held = new global::app.data.@this("windows", list, context: Ctx);

        var closed = await held.Get("on.closed");
        await closed.IsSuccess();
        var desktop = await held.Get("[0]");
        await desktop.IsSuccess();   // [0], the desktop: an index is a window
        var none = await held.Get("[7]");
        await Assert.That((await none.Value())?.IsNull ?? true).IsTrue();   // no window 7: nothing (not found)
    }

    [Test] public async Task ACoreItem_HasNoClick_AndANameItsTypeDoesntDeclareIsRefused()
    {
        var text = (global::app.type.item.text.@this)"plain";
        var window = new Window();

        await Assert.That(text.on.Names).DoesNotContain("click");
        await Assert.That(text.on["closed"]).IsNull();
        await Assert.That(window.on["click"]).IsNull();

        var held = new global::app.data.@this("w", window, context: Ctx);
        var refused = await (await held.Get("on")).Get("click");
        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("EventNotFound");
        await Assert.That(refused.Error.Message).Contains("closed");
    }
}
