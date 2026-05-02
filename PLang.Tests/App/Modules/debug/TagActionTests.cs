using global::App.modules.debug;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Modules.debug;

public class TagActionTests
{
    [Test]
    public async Task Tag_PairsForm_MergesIntoCurrentTags()
    {
        await using var app = new global::App.@this("/app");
        await using var call = app.Debug.CallStack.Push(MakeAction("Goal"));
        var action = new Tag
        {
            Context = app.Context,
            Pairs = new global::App.Data.@this<Dictionary<string, string>>(
                "Pairs",
                new Dictionary<string, string> { ["k1"] = "v1", ["k2"] = "v2" })
        };
        await action.Run();

        await Assert.That(call.Tags).IsNotNull();
        await Assert.That(call.Tags!["k1"]).IsEqualTo("v1");
        await Assert.That(call.Tags!["k2"]).IsEqualTo("v2");
    }

    [Test]
    public async Task Tag_LabelForm_SetsTagsLabelTrue()
    {
        await using var app = new global::App.@this("/app");
        await using var call = app.Debug.CallStack.Push(MakeAction("Goal"));
        var action = new Tag
        {
            Context = app.Context,
            Label = new global::App.Data.@this<string>("Label", "manual-checkpoint")
        };
        await action.Run();

        await Assert.That(call.Tags).IsNotNull();
        await Assert.That(call.Tags!["manual-checkpoint"]).IsEqualTo("true");
    }

    [Test]
    public async Task Tag_NoOpWhenCurrentNull()
    {
        await using var app = new global::App.@this("/app");
        // No Push — Current is null.
        var action = new Tag
        {
            Context = app.Context,
            Label = new global::App.Data.@this<string>("Label", "x")
        };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Tag_AllocatesTagsDict_WhenNull()
    {
        await using var app = new global::App.@this("/app");
        await using var call = app.Debug.CallStack.Push(MakeAction("Goal"));
        await Assert.That(call.Tags).IsNull();

        var action = new Tag
        {
            Context = app.Context,
            Label = new global::App.Data.@this<string>("Label", "x")
        };
        await action.Run();
        await Assert.That(call.Tags).IsNotNull();
    }

    [Test]
    public async Task Tag_ActionIsNotCacheable()
    {
        var attr = typeof(Tag).GetCustomAttributes(typeof(global::App.modules.ActionAttribute), false)
            .Cast<global::App.modules.ActionAttribute>().FirstOrDefault();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Cacheable).IsFalse();
    }
}
