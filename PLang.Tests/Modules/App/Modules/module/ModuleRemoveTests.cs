using app.type.item.variable;
using app.module.module;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.module;

/// A module taken out answers what happened: true when it was taken out, false when the app held it no longer, the
/// refusal (ModuleShared) when it is the module of the app this one was made from — never a throw.
public class ModuleRemoveTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Remove_ExistingModule_Succeeds()
    {
        // "variable" is a built-in module
        await Assert.That((await _app.module.Get("variable", _app.actor.list.User.Context)).Success).IsTrue();

        var action = new Remove(_app.actor.list.User.Context) { Name = (global::app.type.item.text.@this)"variable" };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await _app.module.Get("variable", _app.actor.list.User.Context)).Success).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentModule_ReturnsNotFound()
    {
        var action = new Remove(_app.actor.list.User.Context) { Name = (global::app.type.item.text.@this)"nonexistent" };
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(404);
    }

    [Test]
    public async Task Remove_ThenActions_NotResolvable()
    {
        // Hold the element from THIS app's registry, taken before removal — an action that was
        // already built still carries its module, so this asks the question removal must answer.
        var held = _app.Module("variable");
        var action = new Remove(_app.actor.list.User.Context) { Name = (global::app.type.item.text.@this)"variable" };
        await action.Start();

        var (resolved, error) = (new PrAction(_app.type.list) { Module = held, Name = "set" }).Instance(_app.actor.list.User.Context);
        await Assert.That(resolved).IsNull();
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task AModuleTakenOut_IsTrue_AndAgain_IsFalse()
    {
        var module = _app.module.Named("variable")!;

        var removed = await module.Remove(_app.actor.list.System.Context);
        await removed.IsSuccess();
        await Assert.That((await removed.Value())!.Value).IsTrue();
        await Assert.That(_app.module.Named("variable")).IsNull();

        var again = await module.Remove(_app.actor.list.System.Context);
        await again.IsSuccess();
        await Assert.That((await again.Value())!.Value).IsFalse();
    }

    [Test]
    public async Task AModuleOfTheMaker_IsRefused_AndStays()
    {
        await using var sub = new global::app.@this("/app/sub", _app.actor.list.User).Testing();
        var module = sub.module.Named("variable") ?? _app.module.Named("variable");

        var refused = await module!.Remove(sub.actor.list.System.Context);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("ModuleShared");
        await Assert.That(_app.module.Named("variable")).IsNotNull();
    }
}
