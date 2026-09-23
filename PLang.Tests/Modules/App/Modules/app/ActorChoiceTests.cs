namespace PLang.Tests.App.Modules.app;

/// <summary>
/// An actor slot names WHO — a choice from the closed set {system, user} (<c>choice&lt;actor&gt;</c>).
/// Each handler's slot is bound from a .pr-shaped action and read through the typed door; the name
/// selects the live actor through <c>app.Actor[name]</c>.
/// </summary>
public class ActorChoiceTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // The handler bound from an action holding Actor=<raw> — the typed views the run reads.
    private async Task<T> Bound<T>(string module, string action, object? actor, params (string, object?)[] required) where T : class
    {
        var node = global::PLang.Tests.Shared.Make.Action(module, action,
            required.Prepend(("Actor", actor)).ToArray());
        var (handler, error) = await node.Bind(Ctx);
        await Assert.That(error).IsNull();
        return (T)handler!;
    }

    private async Task SelectsSystem(global::app.data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>? slot)
    {
        var named = await slot!.Value();
        await Assert.That(named).IsNotNull();
        await Assert.That(ReferenceEquals(_app.Actor[named!], _app.System)).IsTrue();
    }

    [Test] public async Task GoalCall_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.action.goal.Call>("goal", "call", "system")).Actor);

    [Test] public async Task EventOn_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.action.@event.On>("event", "on", "system", ("Trigger", "BeforeGoal"))).Actor);

    [Test] public async Task EnvironmentRun_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.action.environment.run>("environment", "run", "system")).Actor);

    [Test] public async Task ChannelSet_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.action.channel.Set>("channel", "set", "system")).Actor);

    [Test] public async Task ChannelRemove_System_SelectsSystem()
        => await SelectsSystem((await Bound<global::app.module.action.channel.Remove>("channel", "remove", "system")).Actor);

    [Test]
    public async Task GoalCall_SystemActor_RunsOnSystemContext()
    {
        _app.Goal.Add(new global::app.goal.@this
        {
            Name = "TestGoal",
            Path = global::app.type.item.path.@this.Resolve("/TestGoal.goal", global::PLang.Tests.TestApp.SharedContext)
        });
        var action = new global::app.module.action.goal.Call(Ctx)
        {
            Name = new global::app.type.item.text.@this("TestGoal"),
            Actor = new global::app.type.item.choice.@this<global::app.actor.Name>(global::app.actor.Name.system),
            Parameter = new global::app.type.item.list.@this(
                new List<Data> { new Data("onSystem", "yes", context: Ctx) }, Ctx),
        };

        await (await action.Run()).IsSuccess();

        await Assert.That((await _app.System.Context.Variable.Get("onSystem")).HasValue).IsTrue();
        await Assert.That((await _app.User.Context.Variable.Get("onSystem")).HasValue).IsFalse();
    }

    [Test]
    public async Task UnknownName_Declines_NamingTheOptions()
    {
        var slot = (await Bound<global::app.module.action.channel.Set>("channel", "set", "service")).Actor!;

        await Assert.That(await slot.Value()).IsNull();
        await Assert.That(slot.Error!.Message).Contains("system");
        await Assert.That(slot.Error!.Message).Contains("user");
    }

    [Test]
    public async Task VariableHoldingAName_Selects()
    {
        await Ctx.Variable.Set("who", "user");
        var slot = (await Bound<global::app.module.action.channel.Set>("channel", "set", "%who%")).Actor!;

        var named = await slot.Value();
        await Assert.That(ReferenceEquals(_app.Actor[named!], _app.User)).IsTrue();
    }

    [Test]
    public async Task VariableHoldingAnActor_Declines()
    {
        await Ctx.Variable.Set("who", _app.System);
        var slot = (await Bound<global::app.module.action.channel.Set>("channel", "set", "%who%")).Actor!;

        await Assert.That(await slot.Value()).IsNull();
        await Assert.That(slot.Success).IsFalse();
    }

    [Test]
    public async Task Registry_ActorStaysTheActorItem_SlotNamesChoiceOfActor()
    {
        await Assert.That(_app.Type["actor"].ClrType).IsEqualTo(typeof(global::app.actor.@this));
        var slot = typeof(global::app.module.action.goal.Call).GetProperty("Actor")!.PropertyType;
        var entity = _app.Type[slot];
        await Assert.That(entity.Name).IsEqualTo("choice");
        await Assert.That(entity.Kind!.Name).IsEqualTo("actor");
    }
}
