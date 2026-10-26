namespace PLang.Tests.App.Modules.snapshot;

public class ResumeTests
{
    [Test]
    public async Task ResumingADict_AnswersTheTypedAsksOwnReason_Whole()
    {
        // a snapshot kept in a .json file reads as a dict unless the step says what it reads (`… as snapshot`): the
        // resume answers why the slot's typed ask declined, as it gave it — never a hand-built rebuild failure
        var app = new global::app.@this("/resume", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var dict = new global::app.type.item.dict.@this(app.type.list[typeof(global::app.type.item.dict.@this)]);
        dict.Set(new global::app.data.@this("actor", "not a snapshot", context: context));
        await context.Variable.Set("snap", new global::app.data.@this("snap", dict, context: context));
        var action = await context.Action("snapshot.resume(Snapshot=%snap%)");

        var result = await action.Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CreateItemDeclined");
        await Assert.That(result.Error.Status.Equals((global::app.type.item.status.@this)400)).IsTrue();
        await Assert.That(result.Error.Message).Contains("holds a dict");
        await Assert.That(result.Error.Message).Contains("a snapshot is made from a snapshot or its text");
        await Assert.That(result.Error is global::app.error.ServiceError).IsFalse();
    }

    [Test]
    public async Task ResumingATextThatHoldsNoSnapshot_AnswersNoSnapshot()
    {
        // a text the snapshot's reader reads as nothing (a db row or file holding `null`) declines with no reason of its
        // own: the resume says it holds none and the step that reads a snapshot
        var app = new global::app.@this("/resume", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", "null");
        var action = await context.Action("snapshot.resume(Snapshot=%snap%)");

        var result = await action.Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoSnapshot");
        await Assert.That(result.Error.Status.Equals((global::app.type.item.status.@this)400)).IsTrue();
        await Assert.That(result.Error.Message).Contains("holds no snapshot");
        await Assert.That(result.Error.Message).Contains("as snapshot");
        await Assert.That(result.Error is global::app.error.ServiceError).IsFalse();
    }
}
