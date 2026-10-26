namespace PLang.Tests.App.Modules.snapshot;

public class ResumeTests
{
    // An app that signs for real, its identity the shared test keypair: a value it signs is one it may go on from.
    private static global::app.@this App()
    {
        var app = new global::app.@this("/resume", global::PLang.Tests.Parent.Actor).TestIdentity();
        app.test.list.Open(app.actor.list.User);
        return app;
    }

    // %snap% holds value, signed by the app as a snapshot is: past the signature, what the resume reads is the value.
    private static async Task<global::app.goal.step.action.@this> Holding(global::app.actor.context.@this context, object value)
    {
        var (signer, _) = context.App.Code.Get<global::app.module.signing.code.ISigning>();
        var signed = await signer!.SignAsync(new global::app.module.signing.sign(context)
            { Data = new global::app.data.@this("snap", value, context: context), View = global::app.View.Snapshot });
        await context.Variable.Set("snap", signed);
        return await context.Action("snapshot.resume(Snapshot=%snap%)");
    }

    [Test]
    public async Task ResumingAnUnsignedValue_IsRefused_BeforeItIsRead()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("snap", "anything");

        var result = await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotUnsigned");
    }

    [Test]
    public async Task ResumingADict_AnswersTheTypedAsksOwnReason_Whole()
    {
        // a snapshot kept in a .json file reads as a dict: the resume answers why the slot's typed ask declined, as it
        // gave it — never a hand-built rebuild failure
        await using var app = App();
        var context = app.actor.list.User.Context;
        var dict = new global::app.type.item.dict.@this(app.type.list[typeof(global::app.type.item.dict.@this)]);
        dict.Set(new global::app.data.@this("actor", "not a snapshot", context: context));

        var result = await (await Holding(context, dict)).Start(context);

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
        // a text the snapshot's reader reads as nothing (a row holding `null`) declines with no reason of its own: the
        // resume says it holds none and where a snapshot is kept
        await using var app = App();
        var context = app.actor.list.User.Context;

        var result = await (await Holding(context, "null")).Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoSnapshot");
        await Assert.That(result.Error.Status.Equals((global::app.type.item.status.@this)400)).IsTrue();
        await Assert.That(result.Error.Message).Contains("holds no snapshot");
        await Assert.That(result.Error.Message).Contains(".plang");
        await Assert.That(result.Error is global::app.error.ServiceError).IsFalse();
    }
}
