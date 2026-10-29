namespace PLang.Tests.App.Events;

// Security probe (snapshot 346, A1): a bound call now carries its authored Actor. A maker's binding whose call names an actor
// (system, user), inherited by a sub app and fired there, runs as the CHILD's actor of that role — in the child's app, never
// the maker's actor, and the maker's app is not written.
public class InheritedActorProbeTests
{
    private static async Task<(global::app.@this maker, global::app.@this sub)> Born(string actor)
    {
        var maker = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = maker.actor.list.User.Context;
        // what the handler saw, kept on the app of the context it ran in
        var log = Make.Goal(ctx, "Log",
            Make.Step("set who", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "!app.who", "variable"), ("Value", "%!app.actor.current.name%"))),
            Make.Step("set where", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "!app.where", "variable"), ("Value", "%!app.name%"))));
        maker.goal.list.Add(log);
        await (await Make.Action(ctx, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "before"),
            ("Action", Make.Action(ctx, "goal", "call", ("Name", "Log"), ("Actor", actor)))).Start(ctx)).IsSuccess();
        var sub = new global::app.@this("/test/sub", maker.actor.list.User);
        return (maker, sub);
    }

    [Test]
    [Arguments("system", "System")]
    [Arguments("user", "User")]
    public async Task AnInheritedBinding_NamingAnActor_RunsAsTheChildsActor_InTheChildsApp(string named, string expected)
    {
        var (maker, sub) = await Born(named);
        await using var _m = maker; await using var _s = sub;
        var subCtx = sub.actor.list.User.Context;
        var run = Make.Goal(subCtx, "Run", Make.Step("set x", Make.Action(subCtx, "variable", "set", Make.Param(subCtx, "Name", "x", "variable"), ("Value", "1"))));
        sub.goal.list.Add(run);

        await run.Start(subCtx);

        var childWho = sub.property["who"] is { } w ? (await w.Value())?.ToString() : null;
        var makerWho = maker.property["who"] is { } m ? (await m.Value())?.ToString() : null;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"INHACTOR named={named} child.who={childWho} maker.who={makerWho}");
        await Assert.That(childWho).IsEqualTo(expected);
        await Assert.That(makerWho).IsNull();
    }
}
