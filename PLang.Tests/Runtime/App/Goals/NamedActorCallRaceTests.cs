namespace PLang.Tests.App.Goals;

/// goal.call with a named actor runs its goal for that actor in the caller's flow: calls made at once from separate
/// flows (parallel iterations, each its own context) each keep their own variables, whatever the others set meanwhile.
public class NamedActorCallRaceTests
{
    [Test]
    public async Task EachCall_ByANamedActor_IsAnsweredByItsOwnArgument()
    {
        await using var app = new global::app.@this("/tmp/namedcallrace", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User.Context;
        // Shown: set %seen% = %message%, sleep 100ms (the others call meanwhile), return %seen%
        var goal = Make.Goal(user, "Shown",
            Make.Step("set %seen% = %message%",
                Make.Action(user, "variable", "set", Make.Param(user, "Name", "seen", "variable"), ("Value", "%message%"))),
            Make.Step("sleep 100ms", Make.Action(user, "timer", "sleep", ("Duration", "100ms"))),
            Make.Step("return %seen%", Make.Action(user, "goal", "return", ("Data", "%seen%"))));
        app.goal.list.Add(goal);

        var runs = await Task.WhenAll(Enumerable.Range(1, 20).Select(async i =>
        {
            using var flow = user.Child(app.actor.list.User, default);
            var wrote = i.ToString();
            var call = Make.Action(flow, "goal", "call", ("Name", "Shown"), ("Actor", "system"),
                ("Parameter", new global::app.type.item.list.@this(
                    [new global::app.data.@this("message", wrote, context: flow)], flow.App.type.list["list"])));
            var answer = await call.Start(flow);
            return (Wrote: wrote, Answered: (await answer.Value())?.ToString());
        }));

        var wrong = runs.Where(run => run.Answered != run.Wrote).Select(run => $"{run.Wrote}→{run.Answered}").ToList();
        await Assert.That(wrong).IsEmpty().Because($"{wrong.Count} of 20 calls were answered with another's argument: {string.Join(", ", wrong)}");
    }
}
