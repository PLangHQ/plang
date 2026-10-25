namespace PLang.Tests.App.Modules.app;

/// <summary>
/// An actor slot names WHO — a choice from the closed set {system, user} (<c>choice&lt;actor&gt;</c>).
/// Each handler's slot is bound from a .pr-shaped action and read through the typed door; the name
/// selects the live actor through <c>app.actor.Get(name, app.actor.list.User.Context)</c>.
/// </summary>
public class ActorChoiceTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // The handler bound from an action holding Actor=<raw> — the typed views the run reads.
    private async Task<T> Bound<T>(string module, string action, object? actor, params (string, object?)[] required) where T : class
    {
        var node = global::PLang.Tests.Shared.Make.Action(Ctx, module, action,
            required.Prepend(("Actor", actor)).ToArray());
        var (handler, error) = await node.Bind(Ctx);
        await Assert.That(error).IsNull();
        return (T)handler!;
    }

    // channel.set requires its Name and Goal — the actor choice is read beside them
    private Task<global::app.module.channel.Set> ChannelSet(object? actor)
        => Bound<global::app.module.channel.Set>("channel", "set", actor, ("Name", "c"), ("Goal", global::PLang.Tests.Shared.Make.Call(Ctx, "G")));

    private async Task SelectsSystem(global::app.data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>? slot)
    {
        var named = await slot!.Value();
        await Assert.That(named).IsNotNull();
        await Assert.That(ReferenceEquals(await (await _app.actor.Get(named!.ToString()!, _app.actor.list.User.Context)).Value(), _app.actor.list.System)).IsTrue();
    }

    [Test] public async Task GoalCall_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.goal.Call>("goal", "call", "system")).Actor);

    [Test] public async Task ChannelSet_System_SelectsSystem()
        => await SelectsSystem((await ChannelSet("system")).Actor);

    [Test] public async Task ChannelRemove_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.channel.Remove>("channel", "remove", "system", ("Name", "c"))).Actor);

    [Test]
    public async Task GoalCall_SystemActor_RunsOnSystemContext()
    {
        // the goal copies its parameter into %seen%, then answers the actor it runs as
        _app.goal.list.Add(await RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, "TestGoal",
            Make.Step("set %seen% = %onSystem%",
                Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "seen", "variable"), Make.Param(Ctx, "Value", "%onSystem%", "variable"))),
            Make.Step("return %!app.actor.current%",
                Make.Action(Ctx, "goal", "return", Make.Param(Ctx, "Data", "%!app.actor.current%", "variable"))))));
        var action = new global::app.module.goal.Call(Ctx)
        {
            Name = new Data("", Ctx.Text("TestGoal"), context: Ctx).As<global::app.goal.@this>(),
            Actor = Ctx.Choice(global::app.actor.Name.system),
            Parameter = new global::app.type.item.list.@this(
                new List<Data> { new Data("onSystem", "yes", context: Ctx) }, Ctx.App.type.list["list"]),
        };

        var ran = await action.Start();
        await ran.IsSuccess();

        // it ran as the actor named, and nothing it set reached the caller's memory
        await Assert.That((await ran.Value()) as global::app.actor.@this).IsSameReferenceAs(_app.actor.list.System);
        await Assert.That((await _app.actor.list.User.Context.Variable.Get("seen")).HasValue).IsFalse();
    }

    [Test]
    public async Task UnknownName_Declines_NamingTheOptions()
    {
        var slot = (await ChannelSet("service")).Actor!;

        await Assert.That(await slot.Value()).IsNull();
        await Assert.That(slot.Error!.Message).Contains("system");
        await Assert.That(slot.Error!.Message).Contains("user");
    }

    [Test]
    public async Task VariableHoldingAName_Selects()
    {
        await Ctx.Variable.Set("who", "user");
        var slot = (await ChannelSet("%who%")).Actor!;

        var named = await slot.Value();
        await Assert.That(ReferenceEquals(await (await _app.actor.Get(named!.ToString()!, _app.actor.list.User.Context)).Value(), _app.actor.list.User)).IsTrue();
    }

    [Test]
    public async Task VariableHoldingAnActor_Declines()
    {
        await Ctx.Variable.Set("who", _app.actor.list.System);
        var slot = (await ChannelSet("%who%")).Actor!;

        await Assert.That(await slot.Value()).IsNull();
        await Assert.That(slot.Success).IsFalse();
    }

    [Test]
    public async Task Registry_ActorStaysTheActorItem_SlotNamesChoiceOfActor()
    {
        await Assert.That(_app.type.list["actor"].ClrType).IsEqualTo(typeof(global::app.actor.@this));
        var slot = typeof(global::app.module.goal.Call).GetProperty("Actor")!.PropertyType;
        var entity = _app.type.list[slot];
        await Assert.That(entity.Name).IsEqualTo("choice");
        await Assert.That(entity.kind.Name).IsEqualTo("actor");
    }
}
