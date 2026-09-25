namespace PLang.Tests.App.CollectionsAreData;

/// <summary>
/// A list element that is itself a list stays ONE element. Only an extend (`add` of a list's items)
/// joins another list's items into this one — a parsed nested array is never flattened.
/// </summary>
public class ListNestingTests
{
    private static global::app.type.item.list.@this Parsed(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return (global::app.type.item.list.@this)new global::app.type.item.serializer.json(TestApp.SharedContext)
            .Parse(doc.RootElement.Clone())!;
    }

    // The action's parameter array as the goal's own .pr writer emits it.
    private static async Task<System.Text.Json.JsonElement> WrittenParameters(global::app.goal.step.action.@this action)
    {
        var goal = global::PLang.Tests.Shared.Make.Goal("Start", "/Start.goal", global::PLang.Tests.Shared.Make.Step("a step", action));
        var serializer = (global::app.channel.serializer.plang.@this)
            TestApp.SharedContext.App.User.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, goal, global::app.View.Store);
        using var doc = System.Text.Json.JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.GetProperty("step")[0].GetProperty("code")[0].GetProperty("property").Clone();
    }

    [Test]
    public async Task ListValuedParameter_IsWrittenAsOneRow()
    {
        var action = global::PLang.Tests.Shared.Make.Action("test", "tag",
            new (string, object?)[] { ("Tags", new List<object?> { "http", "fast" }) });

        var parameters = await WrittenParameters(action);

        await Assert.That(parameters.GetArrayLength()).IsEqualTo(1);
        await Assert.That(parameters[0].GetProperty("name").GetString()).IsEqualTo("Tags");
        await Assert.That(parameters[0].GetProperty("type").GetProperty("name").GetString()).IsEqualTo("list");
        await Assert.That(parameters[0].GetProperty("value").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task GoalCallListArgument_IsWrittenAsOneRow()
    {
        var args = new List<object?> { new Data("to", "x@y.z", context: TestApp.SharedContext) };
        var action = global::PLang.Tests.Shared.Make.Action("goal", "call",
            new (string, object?)[] { ("Name", "SendMail"), ("Parameter", args) });

        var parameters = await WrittenParameters(action);

        await Assert.That(parameters.GetArrayLength()).IsEqualTo(2);
        await Assert.That(parameters[1].GetProperty("name").GetString()).IsEqualTo("Parameter");
        await Assert.That(parameters[1].GetProperty("value").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task AddingAList_Extends_WithoutCopying()
    {
        var target = Parsed("[10,20,30]");
        var more = Parsed("[40,50]");

        target.Add(more);
        await Assert.That(target.CountRaw).IsEqualTo(5);

        // The extend reads the added list's elements in place — a later change to it shows through.
        more.Add(new Data("", 60L, context: TestApp.SharedContext));
        await Assert.That(target.CountRaw).IsEqualTo(6);
    }

    [Test]
    public async Task Flatten_LiftsANestedElement()
    {
        var app = TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "listnest-" + System.Guid.NewGuid().ToString("N")[..6]));
        var ctx = app.User.Context;
        ctx.Variable.Set("l", Parsed("[[1,2],3]"));

        var result = await app.Run(new global::app.module.action.list.Flatten(ctx)
        {
            ListName = new global::app.variable.@this("l"),
        }, ctx);

        await result.IsSuccess();
        var flat = (global::app.type.item.list.@this)(await result.Value())!;
        await Assert.That(flat.CountRaw).IsEqualTo(3);
    }

    [Test]
    public async Task NestedJsonArray_StaysNested_CountsItsElements()
    {
        var list = Parsed("[[1,2],[3,4]]");

        await Assert.That(list.CountRaw).IsEqualTo(2);
        var first = await list.Items(global::PLang.Tests.TestApp.SharedContext).ElementAt(0).Value();
        await Assert.That(first).IsTypeOf<global::app.type.item.list.@this>();
        await Assert.That(((global::app.type.item.list.@this)first!).CountRaw).IsEqualTo(2);
    }
}
