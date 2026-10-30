namespace PLang.Tests.App.Context;

// A frame's own variables, tags and errors are made at their first ask; under parallel branches that ask at once, one
// object is made and every write lands in it.
public class FrameFirstAskTests
{
    // 64 branches write the frame's first local at once: every one lands
    [Test]
    public async Task ParallelFirstWrites_AllLandInOneStore()
    {
        await using var app = new global::app.@this("/cost", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var goal = Make.Goal(global::PLang.Tests.Parent.Actor.Context, "Frame");
        await using var frame = context.call.Push(goal, await context.call.Setting());
        using var gate = new System.Threading.Barrier(64);

        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            gate.SignalAndWait();
            frame.Set($"v{i}", new global::app.data.@this($"v{i}", i, context: context));
        })));

        await Assert.That(Enumerable.Range(0, 64).Where(i => !frame.Holds($"v{i}")).ToList()).IsEmpty();
    }

    // a frame nothing wrote to answers nothing, and each of its three is one object however many ask at once
    [Test]
    public async Task AFrameNothingWroteTo_AnswersNothing_AndEachOfItsThreeIsOneObject()
    {
        await using var app = new global::app.@this("/cost", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var goal = Make.Goal(global::PLang.Tests.Parent.Actor.Context, "Frame");
        await using var frame = context.call.Push(goal, await context.call.Setting());
        await Assert.That(frame.Holds("nope")).IsFalse();
        await Assert.That(frame.TryGet("nope", out _)).IsFalse();
        await Assert.That(frame.Error).IsNull();

        using var gate = new System.Threading.Barrier(32 * 3);
        var asked = await Task.WhenAll(Enumerable.Range(0, 32).SelectMany(_ => new System.Func<object>[]
            { () => frame.Tags, () => frame.Parameter, () => frame.Errors })
            .Select(ask => Task.Run(() => { gate.SignalAndWait(); return ask(); })));

        await Assert.That(asked.Distinct().Count()).IsEqualTo(3);
        await Assert.That(frame.Errors.Count()).IsEqualTo(0);
    }
}
