using app.module.debug;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Modules.debug;

public class TagActionTests
{
    private static Tag Tagging(global::app.@this app, Dictionary<string, object?> tags)
        => new(app.actor.list.User.Context) { Tags = tags.ToDictData(app.actor.list.User.Context) };

    private static int Count(global::app.call.@this call, global::app.@this app)
        => call.Tags.Entries(app.actor.list.User.Context).Count();

    private static global::app.data.@this? Read(global::app.call.@this call, string key, global::app.@this app)
        => call.Tags.Get(key, app.actor.list.User.Context);

    // The frames a running tag action sits in: its goal's, its step's, its own.
    private static (global::app.call.@this goal, global::app.call.@this step, global::app.call.@this action)
        Frames(global::app.@this app)
    {
        var action = MakeAction(app.actor.list.User.Context, "Goal", module: "debug", actionName: "tag");
        var stack = app.actor.list.User.Context.call;
        var goal = stack.Push(action.Step!.Goal!);
        var step = stack.Push(action.Step!);
        return (goal, step, stack.Push(action));
    }

    [Test]
    public async Task Tag_WritesOntoTheGoalsFrame()
    {
        // The tag action's own frame and its step's pop when the step ends; the goal's frame lives for
        // the goal's run, so a later step can read what this one tagged.
        await using var app = new global::app.@this("/app").Testing();
        var (goal, step, action) = Frames(app);
        await using (goal) await using (step) await using (action)
        {
            await Tagging(app, new() { ["k1"] = "v1", ["k2"] = "v2" }).Start();

            await Assert.That(Count(goal, app)).IsEqualTo(2);
            await Assert.That(Read(goal, "k1", app)?.Peek()?.ToString()).IsEqualTo("v1");
            await Assert.That(Read(goal, "k2", app)?.Peek()?.ToString()).IsEqualTo("v2");
            await Assert.That(Count(step, app)).IsEqualTo(0);
            await Assert.That(Count(action, app)).IsEqualTo(0);
        }
    }

    // A bare label is written as the dict {label: true}.
    [Test]
    public async Task Tag_LabelAsTrue_SetsTagsLabelTrue()
    {
        await using var app = new global::app.@this("/app").Testing();
        var (goal, step, action) = Frames(app);
        await using (goal) await using (step) await using (action)
        {
            await Tagging(app, new() { ["manual-checkpoint"] = true }).Start();

            await Assert.That(await Read(goal, "manual-checkpoint", app)!.ToBooleanAsync()).IsTrue();
        }
    }

    [Test]
    public async Task Tag_NoOpWhenCurrentNull()
    {
        await using var app = new global::app.@this("/app").Testing();
        // No Push — Current is null.
        var result = await Tagging(app, new() { ["x"] = true }).Start();
        await result.IsSuccess();
    }

    // Outside any goal, the frame the action runs in is tagged.
    [Test]
    public async Task Tag_OutsideAGoal_TagsTheCurrentFrame()
    {
        await using var app = new global::app.@this("/app").Testing();
        await using var call = app.actor.list.User.Context.call.Push(MakeAction(app.actor.list.User.Context, "Goal"));
        await Tagging(app, new() { ["x"] = true }).Start();

        await Assert.That(Count(call, app)).IsEqualTo(1);
        await Assert.That(await Read(call, "x", app)!.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Tag_MergeOverwritesAKeyAndKeepsTheRest()
    {
        await using var app = new global::app.@this("/app").Testing();
        var (goal, step, action) = Frames(app);
        await using (goal) await using (step) await using (action)
        {
            await Tagging(app, new() { ["a"] = "1", ["b"] = "2" }).Start();
            await Tagging(app, new() { ["b"] = "3" }).Start();

            await Assert.That(Count(goal, app)).IsEqualTo(2);
            await Assert.That(Read(goal, "a", app)?.Peek()?.ToString()).IsEqualTo("1");
            await Assert.That(Read(goal, "b", app)?.Peek()?.ToString()).IsEqualTo("3");
        }
    }

    // A goal's next step reads what its tag step wrote, through %!call.Scope%.
    [Test]
    public async Task Tag_NextStepReadsItThroughScope()
    {
        await using var app = new global::app.@this("/app").Testing();
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(app.actor.list.User.Context, "Tagging",
            Make.Step("tag owner=checkout",
                Make.Action(app.actor.list.User.Context, "debug", "tag", Make.Param(app.actor.list.User.Context, "Tags", new Dictionary<string, object?> { ["owner"] = "checkout" }, "dict"))),
            Make.Step("set %read% = %!call.Scope.Tags.owner%",
                Make.Action(app.actor.list.User.Context, "variable", "set", Make.Param(app.actor.list.User.Context, "Name", "read", "variable"),
                    Make.Param(app.actor.list.User.Context, "Value", "%!call.Scope.Tags.owner%", "variable")))));
        app.goal.list.Add(goal);

        var context = app.actor.list.User.Context;
        var result = await app.Start(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("read"))?.ToString()).IsEqualTo("checkout");
    }

    [Test]
    public async Task Tag_ActionIsNotCacheable()
    {
        var attr = typeof(Tag).GetCustomAttributes(typeof(global::app.module.ActionAttribute), false)
            .Cast<global::app.module.ActionAttribute>().FirstOrDefault();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Cacheable).IsFalse();
    }
}
