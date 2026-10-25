namespace PLang.Tests.App.Goals;

/// Security probe (bugs full pass, context.Run): a goal run for another actor reads nothing of the caller's variables,
/// either way (User → System, System → User), whether or not the caller is itself inside a goal. Asserts the SAFE
/// behaviour: red is one actor reading another's memory.
public class ActorRunMemoryProbeTests
{
    private static async Task<string?> Peek(string callerActor, string runsAs, bool callerInGoal)
    {
        await using var app = new global::app.@this("/tmp/actorrunprobe", global::PLang.Tests.Parent.Actor).Testing();
        var caller = callerActor == "system" ? app.actor.list.System : app.actor.list.User;
        var ctx = caller.Context;
        var goal = Make.Goal(ctx, "Peek",
            Make.Step("return %secret%", Make.Action(ctx, "goal", "return", ("Data", "%secret%"))));
        app.goal.list.Add(goal);

        using var flow = ctx.Child(caller, default);
        await flow.Variable.Set("secret", "CALLERS-SECRET");
        var host = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", flow) };
        await using var inGoal = callerInGoal ? flow.call.Push(host, await flow.call.Setting()) : null;

        var call = Make.Action(flow, "goal", "call", ("Name", "Peek"), ("Actor", runsAs));
        var answer = await call.Start(flow);
        return answer.Success ? (await answer.Value())?.ToString() : "ERR:" + answer.Error?.Key;
    }

    // control: the probe can see a variable when the actor is the caller's own (the goal runs in the caller's memory)
    [Test]
    public async Task Control_TheCallersOwnActor_ReadsTheVariable()
        => await Assert.That(await Peek("user", "user", false) ?? "").Contains("CALLERS-SECRET");

    [Test]
    [Arguments("user", "system", false)]
    [Arguments("user", "system", true)]
    [Arguments("system", "user", false)]
    [Arguments("system", "user", true)]
    public async Task AGoalRunForTheOtherActor_DoesNotReadTheCallersVariable(string caller, string runsAs, bool inGoal)
    {
        var seen = await Peek(caller, runsAs, inGoal);
        await Assert.That(seen ?? "").DoesNotContain("CALLERS-SECRET");
    }
}
