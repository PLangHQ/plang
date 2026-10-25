namespace PLang.Tests.App.Tester;

// A dump never ends the program it watches: a value inside a dumped structure that resolves to a structure holding
// it again (a goal whose action reads %!data%, while %!data% holds that goal — a build's own) is named, not walked.
public class DebugDumpTests
{
    [Test]
    public async Task AValueResolvingToTheStructureItIsIn_IsNamed_NotWalkedForever()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Start",
            Make.Step("set %x% = %!data%", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "%x%", "variable"), ("Value", "%!data%")))));
        await ctx.Variable.Set("!data", ctx.Ok(goal));

        var dumped = await goal.Debug(ctx);

        await Assert.That(dumped).Contains("%!data%");
        await Assert.That(dumped).Contains("Start");
    }
}
