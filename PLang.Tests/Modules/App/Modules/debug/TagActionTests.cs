using app.module.debug;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Modules.debug;

public class TagActionTests
{
    private static Tag Tagging(global::app.@this app, Dictionary<string, object?> tags)
        => new(app.actor.list.User.Context) { Tags = tags.ToDictData(app.actor.list.User.Context) };

    private static int Count(global::app.callstack.call.@this call, global::app.@this app)
        => call.Tags.Entries(app.actor.list.User.Context).Count();

    private static global::app.data.@this? Read(global::app.callstack.call.@this call, string key, global::app.@this app)
        => call.Tags.Get(key, app.actor.list.User.Context);

    [Test]
    public async Task Tag_MergesIntoCallerTags()
    {
        // Real PLang flow: outer scope (goal) has its own Call, then the tag action's
        // dispatch pushes another Call under it. Tag must write to the OUTER (caller),
        // not its own Call which pops immediately when Run returns. Otherwise the next
        // step's assertion can't see the tag.
        await using var app = TestApp.Create("/app");
        await using var outer = app.actor.list.User.CallStack.Push(MakeAction("Goal"));
        await using var tagCall = app.actor.list.User.CallStack.Push(MakeAction("TagDispatch", module: "debug", actionName: "tag"));
        await Tagging(app, new() { ["k1"] = "v1", ["k2"] = "v2" }).Start();

        await Assert.That(Count(outer, app)).IsEqualTo(2);
        await Assert.That(Read(outer, "k1", app)?.Peek()?.ToString()).IsEqualTo("v1");
        await Assert.That(Read(outer, "k2", app)?.Peek()?.ToString()).IsEqualTo("v2");
        // The tag's own Call must NOT have entries — those would vanish on Pop.
        await Assert.That(Count(tagCall, app)).IsEqualTo(0);
    }

    // A bare label is written as the dict {label: true}.
    [Test]
    public async Task Tag_LabelAsTrue_SetsTagsLabelTrue()
    {
        await using var app = TestApp.Create("/app");
        await using var call = app.actor.list.User.CallStack.Push(MakeAction("Goal"));
        await Tagging(app, new() { ["manual-checkpoint"] = true }).Start();

        await Assert.That(await Read(call, "manual-checkpoint", app)!.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Tag_NoOpWhenCurrentNull()
    {
        await using var app = TestApp.Create("/app");
        // No Push — Current is null.
        var result = await Tagging(app, new() { ["x"] = true }).Start();
        await result.IsSuccess();
    }

    [Test]
    public async Task Tag_MergeOverwritesAKeyAndKeepsTheRest()
    {
        await using var app = TestApp.Create("/app");
        await using var call = app.actor.list.User.CallStack.Push(MakeAction("Goal"));
        await Tagging(app, new() { ["a"] = "1", ["b"] = "2" }).Start();
        await Tagging(app, new() { ["b"] = "3" }).Start();

        await Assert.That(Count(call, app)).IsEqualTo(2);
        await Assert.That(Read(call, "a", app)?.Peek()?.ToString()).IsEqualTo("1");
        await Assert.That(Read(call, "b", app)?.Peek()?.ToString()).IsEqualTo("3");
    }

    // The frame's tags are a plang dict — %!callStack.Current.Tags.x% navigates into them.
    [Test]
    public async Task Tag_ReadsBackAsAVariable()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;
        await using var call = app.actor.list.User.CallStack.Push(MakeAction("Goal"));
        await Tagging(app, new() { ["owner"] = "checkout" }).Start();

        var variable = new global::app.type.item.variable.parser.@this("%!callStack.Current.Tags.owner%").Variable.Single();
        var read = await variable.Start(ctx);
        await read.IsSuccess();
        await Assert.That(read.Peek()?.ToString()).IsEqualTo("checkout");
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
