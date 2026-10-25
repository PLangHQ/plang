using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.VariablesTests;

// A variable is set and removed through the variable type's on.set / on.remove: before a set, handed the value about
// to be stored — a failure or a Handled answer is the set's answer and nothing is written; after it, the variable and
// the stored value. A name's first set is a set; the same Data set again changes nothing and fires nothing.
public class CollectionEventsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/CollectionEventsTests-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task AfterSet_IsHandedTheVariable_AndTheStoredValue()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "old");
        string? name = null;
        string? stored = null;
        app.actor.list.User.on.Bind(app.variable.type, "set", When.after, async (item, result, ctx) =>
        {
            name = ((global::app.type.item.variable.@this)item).Name;
            stored = (await result.Value())?.ToString();
            return ctx.Ok();
        }, Scope.app);

        await vars.Set("name", "new");

        await Assert.That(name).IsEqualTo("name");
        await Assert.That(stored).IsEqualTo("new");
    }

    [Test]
    public async Task AFirstSet_IsASet()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var sets = 0;
        app.actor.list.User.on.Bind(app.variable.type, "set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, Scope.app);

        await vars.Set("name", "first");

        await Assert.That(sets).IsEqualTo(1);
    }

    [Test]
    public async Task TheSameDataSetAgain_FiresNothing()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var held = await vars.Set("name", "first");
        var sets = 0;
        app.actor.list.User.on.Bind(app.variable.type, "set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, Scope.app);

        await vars.Set("name", held);

        await Assert.That(sets).IsEqualTo(0);
    }

    [Test]
    public async Task BeforeSet_IsHandedTheValue_AFailureIsTheAnswer_AndNothingIsWritten()
    {
        var vars = new Variables(app.actor.list.User.Context);
        await vars.Set("name", "old");
        string? about = null;
        app.actor.list.User.on.Bind(app.variable.type, "set", When.before, async (_, result, ctx) =>
        {
            about = (await result.Value())?.ToString();
            return ctx.Error(new global::app.error.@this("no", "Refused", 400));
        }, Scope.app);

        var answer = await vars.Set("name", "new");

        await Assert.That(about).IsEqualTo("new");
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("Refused");
        await Assert.That((await vars.Value("name")).ToString()).IsEqualTo("old");
    }

    // A restore is its sets: while one is held by a before-event it has not finished, and says so — it answers when
    // the last variable is written, never before.
    [Test]
    public async Task Restore_DoesNotAnswerUntilItsSetsHaveFinished()
    {
        await using var source = new global::app.@this("/tmp/CollectionEventsTests-src-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
        await source.actor.list.User.Context.Variable.Set("x", 1L);
        var snap = source.Snapshot(source.actor.list.User.Context);

        var gate = new TaskCompletionSource();
        app.actor.list.User.on.Bind(app.variable.type, "set", When.before, async (_, _, ctx) => { await gate.Task; return ctx.Ok(); }, Scope.app);
        var vars = new Variables(app.actor.list.User.Context);

        // the app hands each owner its own section of the snapshot (app/this.Snapshot.cs), as the test does
        var restoring = vars.Restore(snap.Section(vars.Section), app.actor.list.User.Context);

        await Assert.That(restoring.IsCompleted).IsFalse();
        gate.SetResult();
        await restoring;
        await Assert.That((await vars.Value("x")).ToString()).IsEqualTo("1");
    }

    [Test]
    public async Task ACancellingBeforeSet_IsTheAnswer_AndNothingIsWritten()
    {
        var vars = new Variables(app.actor.list.User.Context);
        app.actor.list.User.on.Bind(app.variable.type, "set", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, Scope.app);

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
        app.actor.list.User.on.Bind(app.variable.type, "remove", When.after, async (_, result, ctx) =>
        {
            removed = (await result.Value())?.ToString();
            return ctx.Ok();
        }, Scope.app);

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
        app.actor.list.User.on.Bind(app.variable.type, "remove", When.before,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("no", "Refused", 400))), Scope.app);

        var answer = await vars.Remove("name");

        await answer.IsFailure();
        await Assert.That(vars.Contains("name")).IsTrue();
    }

    [Test]
    public async Task ABindingForAnotherActor_DoesNotFire()
    {
        var vars = new Variables(app.actor.list.User.Context);
        var sets = 0;
        app.actor.list.System.on.Bind(app.variable.type, "set", When.after, (_, _, ctx) => { sets++; return Task.FromResult(ctx.Ok()); }, Scope.actor);

        await vars.Set("name", "first");

        await Assert.That(sets).IsEqualTo(0);
    }
}
