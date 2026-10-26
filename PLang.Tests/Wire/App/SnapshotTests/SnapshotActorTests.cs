namespace PLang.Tests.App.SnapshotTests;

// Every Data a restore puts into live memory answers as the actor whose memory it goes into, never as whoever read the
// snapshot's text: a Data's context is what it resolves and authorizes in.
public class SnapshotActorTests
{
    // a snapshot of User and System, each holding t (what hold makes), and its text
    private static async Task<string> Saved(Func<global::app.@this, global::app.actor.context.@this, Task> hold)
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        await hold(src, src.actor.list.User.Context);
        await hold(src, src.actor.list.System.Context);
        return await src.Snapshot(src.actor.list.User.Context).Saved().Text(src.actor.list.User.Context);
    }

    // a fresh app, the text restored by the actor named
    private static async Task<global::app.@this> Restored(string text, bool bySystem)
    {
        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = bySystem ? dst.actor.list.System.Context : dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);
        return dst;
    }

    private static IEnumerable<global::app.actor.context.@this> Contexts(global::app.@this app)
        => [app.actor.list.User.Context, app.actor.list.System.Context];

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AVariable_AnswersAsItsOwnActor_WhoeverStartedTheRestore(bool bySystem)
    {
        var text = await Saved(async (_, context) => await context.Variable.Set("t", "held"));

        var dst = await Restored(text, bySystem);

        foreach (var context in Contexts(dst))
            await Assert.That((await context.Variable.Get("t")).Context).IsSameReferenceAs(context);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AValueInsideADict_AnswersAsItsOwnActor_WhoeverStartedTheRestore(bool bySystem)
    {
        var text = await Saved(async (src, context) =>
        {
            var dict = new global::app.type.item.dict.@this(src.type.list[typeof(global::app.type.item.dict.@this)]);
            dict.Set(new global::app.data.@this("inner", "held", context: context));
            await context.Variable.Set("t", new global::app.data.@this("t", dict, context: context));
        });

        var dst = await Restored(text, bySystem);

        foreach (var context in Contexts(dst))
        {
            var dict = await (await context.Variable.Get("t")).Value<global::app.type.item.dict.@this>();
            await Assert.That(dict!.Get("inner", context)!.Context).IsSameReferenceAs(context);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AFramesVariable_AnswersAsItsOwnActor_WhoeverStartedTheRestore(bool bySystem)
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var user = src.actor.list.User.Context;
        await using (user.call.Push([new global::app.data.@this("item", "held", context: user)], await user.call.Setting()))
        {
            var text = await src.Snapshot(user).Saved().Text(user);

            var dst = await Restored(text, bySystem);

            var into = dst.actor.list.User.Context;
            await Assert.That(into.call.Root!.TryGet("item", out var item)).IsTrue();
            await Assert.That(item.Context).IsSameReferenceAs(into);
        }
    }
}
