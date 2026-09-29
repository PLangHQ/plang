namespace PLang.Tests.App.Core;

/// <summary>
/// A program property read off a real <c>.pr</c> is shared by every run and holds its value
/// unloaded. It is no Data and has no context: its value can only be read through a run's own
/// Data, born with that run's context.
/// </summary>
public class SharedRowLoadTests
{
    [Test]
    public async Task ProgramProperty_ValueIsReadOnlyThroughARunsData()
    {
        await using var app = new global::app.@this("/tmp/sharedrow-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(app.actor.list.User.Context, "Start",
            Make.Step("set %x% = hello",
                Make.Action(app.actor.list.User.Context, "variable", "set", ("Name", "%x%"), ("Value", "hello")))));

        var property = goal.Step[0].Code[0]["Value"]!;
        var run = property.Data(app.actor.list.User.Context);

        await Assert.That(property.Value).IsNotTypeOf<global::app.data.@this>();
        await Assert.That(ReferenceEquals(run.Context, app.actor.list.User.Context)).IsTrue();
        await Assert.That((await run.Value())?.ToString()).IsEqualTo("hello");
    }
}
