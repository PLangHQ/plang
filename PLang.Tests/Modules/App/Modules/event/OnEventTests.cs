using app.module.action.on;
using When = app.@event.When;

namespace PLang.Tests.App.actions.EventTests;

// on.event binds a call on an item's event; the call reads %!event% (the running event, with %!event!item% and
// %!event!result% for this firing). on.unbind takes the binding off; on.cancel, from a call bound before an event,
// cancels it with a value.
public class OnEventTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/test");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.User.Context;

    private async Task<global::app.@event.binding.@this> Bind(global::app.type.item.@this item, When when, string @event, string goal,
        params (string, object?)[] args)
    {
        var bound = await new OnEvent(Ctx)
        {
            Item = new global::app.data.@this<global::app.type.item.@this>("Item", item, context: Ctx),
            When = (global::app.type.item.choice.@this<When>)when,
            Event = (global::app.type.item.text.@this)@event,
            Action = Make.Call(goal, args),
        }.Start();
        await bound.IsSuccess();
        return (await bound.Value())!;
    }

    private Goal Goal(string name)
    {
        var goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", global::PLang.Tests.TestApp.SharedContext) };
        _app.goal.list.Add(goal);
        return goal;
    }

    private async Task<global::app.data.@this?> Event() => await Ctx.Variable.Get("!event");

    [Test]
    public async Task BeforeEachGoal_BindsOnTheGoalTypesStart_ForThisActor()
    {
        var binding = await Bind(_app.type.list["goal"], When.before, "start", "Log");

        var before = _app.type.list["goal"].on.start.before;
        await Assert.That(before.Count).IsEqualTo(1);
        await Assert.That(before[0]).IsSameReferenceAs(binding);
        await Assert.That(binding.Actor).IsSameReferenceAs(_app.User);
    }

    [Test]
    public async Task AnEventTheItemHasNot_IsTheError()
    {
        var bound = await new OnEvent(Ctx)
        {
            Item = new global::app.data.@this<global::app.type.item.@this>("Item", _app.type.list["goal"], context: Ctx),
            When = (global::app.type.item.choice.@this<When>)When.before,
            Event = (global::app.type.item.text.@this)"explode",
            Action = Make.Call("Log"),
        }.Start();

        await bound.IsFailure();
        await Assert.That(bound.Error!.Key).IsEqualTo("EventNotFound");
    }

    [Test]
    public async Task AfterAGoal_TheCallRuns_WithItsArguments()
    {
        Goal("AfterCallback");
        var main = Goal("MainGoal");
        await Bind(main, When.after, "start", "AfterCallback", ("callbackRan", true));

        await Make.Call("MainGoal").Start(Ctx);

        var ran = await Ctx.Variable.Get("callbackRan");
        await Assert.That((await ran!.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task TheCall_ReadsTheEvent_AndTheItemItFiredFor()
    {
        Goal("Watch");
        var target = Goal("Target");
        await Bind(_app.type.list["goal"], When.before, "start", "Watch");

        await Make.Call("Target").Start(Ctx);

        var @event = await Event();
        await Assert.That(@event!.Peek()).IsTypeOf<global::app.@event.on.start>();
        await Assert.That(await @event.Properties.Value("item")).IsSameReferenceAs(target);
        // plang reads the firing's own facts as properties: %!event!item%
        var item = await new global::app.type.item.variable.@this("!event!item").Start(Ctx);
        await Assert.That(await item.Value()).IsSameReferenceAs(target);
    }

    [Test]
    public async Task AfterAnAction_TheCallReadsItsResult()
    {
        Goal("Watch");
        // every action of this app (Make.Action's actions belong to the shared test app's modules)
        await Bind(_app.type.list["action"], When.after, "start", "Watch");

        var goal = Goal("Main");
        var step = new Step { Goal = goal, Index = 0, Text = "set %x% = 1" };
        goal.Step.Add(step);
        var set = Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", "one"));
        set.Step = step;
        step.Code.Add(set);
        await step.Start(Ctx);

        var @event = await Event();
        // the item is the action that ran: the one the step holds
        await Assert.That(await @event!.Properties.Value("item")).IsSameReferenceAs(step.Code[0]);
        await Assert.That(await @event.Properties.Value("result")).IsNotNull();
    }

    [Test]
    public async Task Unbind_TakesTheBindingOff()
    {
        var binding = await Bind(_app.type.list["goal"], When.before, "start", "Log");

        var unbound = await new OnUnbind(Ctx)
        {
            Binding = new global::app.data.@this<global::app.@event.binding.@this>("Binding", binding, context: Ctx),
        }.Start();

        await unbound.IsSuccess();
        await Assert.That(_app.type.list["goal"].on.start.before.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ABinding_FiresOnlyWhileItsActorRuns()
    {
        Goal("Watch");
        var target = Goal("Target");
        await Bind(target, When.before, "start", "Watch", ("watched", true));

        await Make.Call("Target").Start(_app.System.Context);
        await Assert.That((await _app.System.Context.Variable.Get("watched"))?.IsInitialized ?? false).IsFalse();

        // the same goal under the actor that bound it: it fires
        await Make.Call("Target").Start(Ctx);
        await Assert.That((await Ctx.Variable.Get("watched"))?.IsInitialized ?? false).IsTrue();
    }

    [Test]
    public async Task Cancel_AnswersItsValue_MarkedHandled()
    {
        var cancelled = await new OnCancel(Ctx) { Value = new global::app.data.@this("", "override-value", context: Ctx) }.Start();

        await cancelled.IsSuccess();
        await Assert.That(cancelled.Handled).IsTrue();
        await Assert.That((await cancelled.Value())?.ToString()).IsEqualTo("override-value");
    }

    [Test]
    public async Task Cancel_WithNoValue_AnswersEmpty_MarkedHandled()
    {
        var cancelled = await new OnCancel(Ctx).Start();

        await cancelled.IsSuccess();
        await Assert.That(cancelled.Handled).IsTrue();
    }
}
