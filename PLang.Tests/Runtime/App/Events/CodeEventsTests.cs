namespace PLang.Tests.App.Events;

// The events C# binds — coverage, a mock, the test runner's step timing, debug's watch on a variable — are the code
// kind: each runs its handler when its moment comes, and a snapshot never keeps one (its maker binds it again).
public class CodeEventsTests
{
    [Test] public async Task TheCSharpEvents_Run_AndNoneIsInASnapshot()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "code-events-" + Guid.NewGuid().ToString("N")[..8]),
            global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        var context = app.actor.list.User.Context;
        var user = app.actor.list.User;
        var ran = new List<string>();

        // coverage, on every action's start
        var coverage = new global::app.test.Coverage();
        coverage.Watch(context);
        // a mock: answers in variable.set's place
        user.@event.Add(new global::app.@event.code.@this(app.Module("variable")["set"]!, "start", global::app.@event.When.before, (_, _, ctx) =>
        {
            var mocked = ctx.Ok("mocked");
            mocked.Handled = true;
            return Task.FromResult(mocked);
        }, user, global::app.@event.code.@this.Always));
        // the test runner's timing, after every step
        user.@event.Add(new global::app.@event.code.@this(app.type.list["step"], "start", global::app.@event.When.after,
            (_, result, _) => { ran.Add("timed"); return Task.FromResult(result); }, user, global::app.@event.code.@this.Always));
        // debug's watch, on one variable's name
        var watched = new global::app.type.item.variable.parser.@this("%count%", app.type.list).Variable.Single();
        user.@event.Add("count", new global::app.@event.code.@this(watched, "set", global::app.@event.When.after,
            (_, result, _) => { ran.Add("watched"); return Task.FromResult(result); }, user, global::app.@event.code.@this.Always));

        var goal = Make.Goal(context, "Main", Make.Step("set %x% = one",
            Make.Action(context, "variable", "set", Make.Param(context, "Name", "x", "variable"), ("Value", "one"))));
        var result = await goal.Start(context);
        await context.Variable.Set("count", 1);

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("mocked");
        await Assert.That(coverage.ModuleActions.Contains(("variable", "set"))).IsTrue();
        await Assert.That(ran).IsEquivalentTo(new[] { "timed", "watched" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var saved = await (await app.Snapshot(context).Saved()).Text(context);
        await Assert.That(user.@event.Items().Count(@event => @event.IsSaved)).IsEqualTo(0);
        await Assert.That(saved).DoesNotContain("\"before\"");
        await Assert.That(saved).DoesNotContain("\"after\"");
    }
}
