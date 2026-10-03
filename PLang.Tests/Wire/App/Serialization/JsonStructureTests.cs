namespace PLang.Tests.App.Serialization;

// A json value writes what it is through the writer — the writer says how it looks. A list navigated out of an indented
// json document prints in a text template as an equal plang list does, never with the document's own spacing; relayed
// as json it reads back the same.
public class JsonStructureTests
{
    private const string Indented = "{\n  \"items\": [\n    \"a\",\n    2,\n    true\n  ]\n}";

    private static async Task<string> Set(global::app.actor.context.@this ctx, string name, object? value)
    {
        var set = Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", name, "variable"), ("Value", value));
        await (await set.Start(ctx)).IsSuccess();
        return (await (await ctx.Variable.Get(name)).Value())?.ToString() ?? "";
    }

    [Test]
    public async Task ANavigatedJsonList_PrintsInATemplateAsAnEqualPlangList()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var answer = await ctx.App.type.list.Kind("json").Decode(System.Text.Encoding.UTF8.GetBytes(Indented), ctx);
        answer.Name = "answer";
        await ctx.Variable.Set(answer);
        await ctx.Variable.Set("plain", new List<object?> { "a", 2L, true });

        var fromJson = await Set(ctx, "got", "The server got: %answer.items%");
        var fromPlang = await Set(ctx, "had", "The server got: %plain%");

        await Assert.That(fromJson).IsEqualTo(fromPlang);
        await Assert.That(fromJson).DoesNotContain("\n");
    }

    [Test]
    public async Task AJsonValue_RelayedAsJson_ReadsBackTheSame()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var json = ctx.App.type.list.Kind("json");
        var answer = await json.Decode(System.Text.Encoding.UTF8.GetBytes(Indented), ctx);

        using var stream = new System.IO.MemoryStream();
        await (await json.Encode(stream, answer, ctx)).IsSuccess();
        var relayed = System.Text.Json.JsonDocument.Parse(stream.ToArray()).RootElement;

        var items = relayed.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(3);
        await Assert.That(items[0].GetString()).IsEqualTo("a");
        await Assert.That(items[1].GetInt64()).IsEqualTo(2L);
        await Assert.That(items[2].GetBoolean()).IsTrue();
    }
}
