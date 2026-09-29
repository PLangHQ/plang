using System.Text.Json.Nodes;

namespace PLang.Tests.App.Serialization;

// The twin of a row's "variable" list: every golden row python wrote (tools/decider/variables.py via
// formal.py) holds exactly the list the C# parser makes of its value — a template's texts, each variable
// once, first written first; a variable slot, the variable it names. A row holding none has no list.
public class VariableListTwinTests
{
    [Test]
    public async Task EveryGoldenRow_ListsWhatTheParserFinds()
    {
        var differ = new List<string>();
        var rows = 0;
        foreach (var entry in FormalWriterTests.Golden())
            foreach (var row in Rows(JsonNode.Parse(entry.GetProperty("pr").GetRawText())))
            {
                rows++;
                var expected = Expected(row);
                var written = row["variable"]?.ToJsonString();
                if (expected != written)
                    differ.Add($"{entry.GetProperty("goal").GetString()}[{entry.GetProperty("index").GetInt32()}] {row["name"]}: python {written ?? "(none)"}, C# {expected ?? "(none)"}");
            }
        await Assert.That(rows).IsGreaterThan(100);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // A variable with an index — a number or a variable key — reads back from the .pr it was written to.
    [Test]
    public async Task AnIndexedVariable_ReadsBackFromThePr_AndRuns()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Indexed",
            Make.Step("set %first% = %users[0].name%",
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "first", "variable"), Make.Param(ctx, "Value", "%users[0].name%", "variable"))),
            Make.Step("set %picked% = %users[%i%].name%",
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "picked", "variable"), Make.Param(ctx, "Value", "%users[%i%].name%", "variable")))));
        await ctx.Variable.Set("users", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "a" },
            new Dictionary<string, object?> { ["name"] = "b" },
        });
        await ctx.Variable.Set("i", 1L);

        await (await goal.Step[0].Start(ctx)).IsSuccess();
        await (await goal.Step[1].Start(ctx)).IsSuccess();

        await Assert.That((await ctx.Variable.GetValue("first"))?.ToString()).IsEqualTo("a");
        await Assert.That((await ctx.Variable.GetValue("picked"))?.ToString()).IsEqualTo("b");
    }

    // Every {name, type, value} row, at any depth.
    private static IEnumerable<JsonObject> Rows(JsonNode? node)
    {
        if (node is JsonArray array)
            foreach (var e in array) foreach (var r in Rows(e)) yield return r;
        if (node is not JsonObject obj) yield break;
        if (obj["type"] is JsonObject && obj.ContainsKey("value") && obj.ContainsKey("name")) yield return obj;
        foreach (var kv in obj) foreach (var r in Rows(kv.Value)) yield return r;
    }

    private static string? Expected(JsonObject row)
    {
        var type = (JsonObject)row["type"]!;
        var holds = type["template"] != null || type["name"]?.GetValue<string>() == "variable";
        if (!holds) return null;
        var found = new List<global::app.type.item.variable.@this>();
        Strings(row["value"], s => found.AddRange(new global::app.type.item.variable.parser.@this(s).Variable));
        if (found.Count == 0) return null;
        using var ms = new System.IO.MemoryStream();
        using (var utf8 = new System.Text.Json.Utf8JsonWriter(ms,
                   new System.Text.Json.JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            new global::app.type.item.variable.serializer.Entry().Write(
                new global::app.type.item.kind.json.Writer(utf8), found.DistinctBy(v => v.Text).ToList());
        return JsonNode.Parse(ms.ToArray())!.ToJsonString();
    }

    private static void Strings(JsonNode? node, Action<string> each)
    {
        switch (node)
        {
            case JsonArray a: foreach (var e in a) Strings(e, each); break;
            case JsonObject o when o["module"] == null: foreach (var kv in o) Strings(kv.Value, each); break;
            case JsonValue v when v.TryGetValue<string>(out var s): each(s); break;
        }
    }
}
