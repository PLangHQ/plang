using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.VariablesTests;

// A variable is set and removed through the variable type's on.set / on.remove: before a set, handed the value about
// to be stored — a failure or a Handled answer is the set's answer and nothing is written; after it, the variable and
// the stored value. A name's first set is a set; the same Data set again changes nothing and fires nothing.
public class CollectionEventsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/CollectionEventsTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.@event.on.own On => app.variable.Own();

    [Test]
    public async Task AfterSet_IsHandedTheVariable_AndTheStoredValue()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "old");
        string? name = null;
        string? stored = null;
        On.Bind("set", When.after, async (item, result, ctx) =>
        {
            name = ((global::app.type.item.variable.@this)item).Name;
            stored = (await result.Value())?.ToString();
            return ctx.Ok();
        }, app.actor.list.User, Scope.app);

        await vars.Set("name", "new");

        await Assert.That(name).IsEqualTo("name");
        await Assert.That(stored).IsEqualTo("new");
    }

    [Test]
    public async Task AFirstSet_IsASet()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var sets = 0;
        On.Bind("set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, app.actor.list.User, Scope.app);

        await vars.Set("name", "first");

        await Assert.That(sets).IsEqualTo(1);
    }

    [Test]
    public async Task TheSameDataSetAgain_FiresNothing()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var held = await vars.Set("name", "first");
        var sets = 0;
        On.Bind("set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, app.actor.list.User, Scope.app);

        await vars.Set("name", held);

        await Assert.That(sets).IsEqualTo(0);
    }

    [Test]
    public async Task BeforeSet_IsHandedTheValue_AFailureIsTheAnswer_AndNothingIsWritten()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "old");
        string? about = null;
        On.Bind("set", When.before, async (_, result, ctx) =>
        {
            about = (await result.Value())?.ToString();
            return ctx.Error(new global::app.error.Error("no", "Refused", 400));
        }, app.actor.list.User, Scope.app);

        var answer = await vars.Set("name", "new");

        await Assert.That(about).IsEqualTo("new");
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("Refused");
        await Assert.That((await vars.Value("name")).ToString()).IsEqualTo("old");
    }

    [Test]
    public async Task ACancellingBeforeSet_IsTheAnswer_AndNothingIsWritten()
    {
        var vars = new Variables(app.actor.list.User.Context);
        On.Bind("set", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.actor.list.User, Scope.app);

        var answer = await vars.Set("name", "new");

        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(vars.Contains("name")).IsFalse();
    }

    [Test]
    public async Task AfterRemove_IsHandedTheRemovedValue()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "ingi");
        string? removed = null;
        On.Bind("remove", When.after, async (_, result, ctx) =>
        {
            removed = (await result.Value())?.ToString();
            return ctx.Ok();
        }, app.actor.list.User, Scope.app);

        var answer = await vars.Remove("name");

        await answer.IsSuccess();
        await Assert.That(removed).IsEqualTo("ingi");
        await Assert.That(vars.Contains("name")).IsFalse();
    }

    [Test]
    public async Task ARefusingBeforeRemove_IsTheAnswer_AndTheVariableStays()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "ingi");
        On.Bind("remove", When.before,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", "Refused", 400))), app.actor.list.User, Scope.app);

        var answer = await vars.Remove("name");

        await answer.IsFailure();
        await Assert.That(vars.Contains("name")).IsTrue();
    }

    [Test]
    public async Task ABindingForAnotherActor_DoesNotFire()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var sets = 0;
        On.Bind("set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, app.actor.list.System, Scope.actor);

        await vars.Set("name", "first");

        await Assert.That(sets).IsEqualTo(0);
    }
}
