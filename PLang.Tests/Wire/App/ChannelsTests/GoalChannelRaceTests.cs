using EngineGoal = global::app.goal.@this;

namespace PLang.Tests.App.ChannelsTests;

/// A write to a goal channel runs its goal the way goal.call runs one: in the writer's own flow. Writes made at once
/// from separate flows (parallel iterations, each its own context) each see their own %message% and set their own
/// variables, whatever the others write meanwhile.
public class GoalChannelRaceTests
{
    // Shown: set %seen% = %message%, sleep 100ms (the others write meanwhile), return %seen%
    private static async Task<global::app.channel.type.goal.@this> Shown(global::app.@this app)
    {
        var ctx = app.actor.list.User.Context;
        var goal = Make.Goal(ctx, "Shown",
            Make.Step("set %seen% = %message%",
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "seen", "variable"), ("Value", "%message%"))),
            Make.Step("sleep 100ms", Make.Action(ctx, "timer", "sleep", ("Duration", "100ms"))),
            Make.Step("return %seen%", Make.Action(ctx, "goal", "return", ("Data", "%seen%"))));
        app.goal.list.Add(goal);
        return await Make.GoalChannel("shown", Make.Call(ctx, goal.Name), app.actor.list.User);
    }

    // twenty writes at once, each from a flow of its own on the user — as a parallel foreach's iterations are
    private static async Task<(string Wrote, string? Answered, string? Seen)[]> Twenty(global::app.@this app,
        global::app.channel.type.goal.@this shown)
    {
        var user = app.actor.list.User.Context;
        return await Task.WhenAll(Enumerable.Range(1, 20).Select(async i =>
        {
            using var flow = user.Child(app.actor.list.User, default);
            var wrote = i.ToString();
            var answer = await shown.Write(new global::app.data.@this("item", wrote, context: flow));
            var seen = await flow.Variable.Get("seen");
            return (wrote, (await answer.Value())?.ToString(), seen.IsInitialized ? (await seen.Value())?.ToString() : null);
        }));
    }

    [Test]
    public async Task EachWrite_IsAnsweredByItsOwnMessage()
    {
        await using var app = new global::app.@this("/tmp/goalrace-a", global::PLang.Tests.Parent.Actor).Testing();
        var runs = await Twenty(app, await Shown(app));
        var wrong = runs.Where(run => run.Answered != run.Wrote).Select(run => $"{run.Wrote}→{run.Answered}").ToList();
        await Assert.That(wrong).IsEmpty().Because($"{wrong.Count} of 20 writes were answered with another's message: {string.Join(", ", wrong)}");
    }

    [Test]
    public async Task EachWrite_SetsItsOwnWritersVariables()
    {
        await using var app = new global::app.@this("/tmp/goalrace-b", global::PLang.Tests.Parent.Actor).Testing();
        var runs = await Twenty(app, await Shown(app));
        var wrong = runs.Where(run => run.Seen != run.Wrote).Select(run => $"{run.Wrote}→{run.Seen ?? "nothing"}").ToList();
        await Assert.That(wrong).IsEmpty().Because($"{wrong.Count} of 20 writers' %seen% was another's: {string.Join(", ", wrong)}");
    }
}
