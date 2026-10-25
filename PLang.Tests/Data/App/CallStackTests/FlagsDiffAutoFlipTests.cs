namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// The diff-capture scope. Error handling opens one so handler-time mutations land on the
/// CallStack's diff stream and Variables.SnapshotAt can project back to throw-time state;
/// the scope itself belongs to the CallStack, which owns the flag, the stream and the
/// subscriptions.
/// </summary>
public class FlagsDiffAutoFlipTests
{
    [Test]
    public async Task Diff_IsOn_InsideADiffScope()
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        await Assert.That((await app.actor.list.User.Context.call.Diff()).Value).IsFalse();

        using (app.actor.list.User.Context.call.DiffScope(app.actor.list.User.Context.Variable))
        {
            await Assert.That((await app.actor.list.User.Context.call.Diff()).Value).IsTrue();
        }
    }

    [Test]
    public async Task Diff_RestoredToPriorState_WhenTheScopeCloses()
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        // Off baseline.
        await Assert.That((await app.actor.list.User.Context.call.Diff()).Value).IsFalse();

        using (app.actor.list.User.Context.call.DiffScope(app.actor.list.User.Context.Variable)) { /* scoped */ }
        await Assert.That((await app.actor.list.User.Context.call.Diff()).Value).IsFalse();

        // Now with Diff already on — the scope must not turn it off afterwards.
        (await app.actor.list.User.Context.call.Setting()).Diff = new global::app.call.setting.diff.@this(new global::app.type.@this(typeof(global::app.call.setting.diff.@this))) { Enabled = true };
        using (app.actor.list.User.Context.call.DiffScope(app.actor.list.User.Context.Variable)) { /* scoped */ }
        await Assert.That((await app.actor.list.User.Context.call.Diff()).Value).IsTrue();
    }
}
