using app.module.debug;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Modules.debug;

public class TagActionTests
{
    [Test]
    public async Task Tag_PairsForm_MergesIntoCallerTags()
    {
        // Real PLang flow: outer scope (goal) has its own Call, then the tag action's
        // dispatch pushes another Call under it. Tag must write to the OUTER (caller),
        // not its own Call which pops immediately when Run returns. Otherwise the next
        // step's assertion can't see the tag.
        await using var app = new global::app.@this("/app");
        await using var outer = app.CallStack.Push(MakeAction("Goal"));
        await using var tagCall = app.CallStack.Push(MakeAction("TagDispatch", module: "debug", actionName: "tag"));
        var action = new Tag
        {
            Context = app.User.Context,
            Pairs = new global::app.data.@this<Dictionary<string, string>>(
                "Pairs",
                new Dictionary<string, string> { ["k1"] = "v1", ["k2"] = "v2" })
        };
        await action.Run();

        await Assert.That(outer.Tags.Count).IsEqualTo(2);
        await Assert.That(outer.Tags["k1"]).IsEqualTo("v1");
        await Assert.That(outer.Tags["k2"]).IsEqualTo("v2");
        // The tag's own Call must NOT have entries — those would vanish on Pop.
        await Assert.That(tagCall.Tags.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Tag_LabelForm_SetsTagsLabelTrue()
    {
        await using var app = new global::app.@this("/app");
        await using var call = app.CallStack.Push(MakeAction("Goal"));
        var action = new Tag
        {
            Context = app.User.Context,
            Label = new global::app.data.@this<string>("Label", "manual-checkpoint")
        };
        await action.Run();

        await Assert.That(call.Tags["manual-checkpoint"]).IsEqualTo("true");
    }

    [Test]
    public async Task Tag_NoOpWhenCurrentNull()
    {
        await using var app = new global::app.@this("/app");
        // No Push — Current is null.
        var action = new Tag
        {
            Context = app.User.Context,
            Label = new global::app.data.@this<string>("Label", "x")
        };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Tag_StartsEmpty_WriteGoesThroughTagsType()
    {
        await using var app = new global::app.@this("/app");
        await using var call = app.CallStack.Push(MakeAction("Goal"));
        await Assert.That(call.Tags.Count).IsEqualTo(0);

        var action = new Tag
        {
            Context = app.User.Context,
            Label = new global::app.data.@this<string>("Label", "x")
        };
        await action.Run();
        await Assert.That(call.Tags.Count).IsEqualTo(1);
        await Assert.That(call.Tags["x"]).IsEqualTo("true");
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
