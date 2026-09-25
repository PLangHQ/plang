namespace PLang.Tests.App.Serialization;

// The formal writer against the python reference: each golden step's .pr rows, read through the real step
// reader, written through formal.Writer, must be python's formal byte for byte (formal_golden.json, written
// by tools/decider/formal_fixture.py from formal.py).
public class FormalWriterTests
{
    internal static System.Text.Json.JsonElement[] Golden()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_golden.json")))
            dir = dir.Parent;
        var path = System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_golden.json");
        return System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement.EnumerateArray().ToArray();
    }

    internal static global::app.goal.@this Goal()
    {
        var context = global::PLang.Tests.TestApp.SharedContext;
        var path = global::app.type.item.path.@this.Resolve("/formal.goal", context);
        return global::app.goal.@this.Parse("Formal\n- a step\n", path, context)!;
    }

    // The step as the .pr holds it: {index, text, code: [rows]} — read through the step reader.
    internal static global::app.goal.step.@this Step(global::app.goal.@this goal, System.Text.Json.JsonElement entry)
    {
        var json = "{\"index\":" + entry.GetProperty("index").GetInt32()
                   + ",\"text\":" + System.Text.Json.JsonSerializer.Serialize(entry.GetProperty("text").GetString())
                   + ",\"code\":" + entry.GetProperty("pr").GetRawText() + "}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8, bytes);
        return (global::app.goal.step.@this)new global::app.goal.step.serializer.Reader(goal)
            .Read(ref reader, null, new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext, "plang"));
    }

    internal static async Task<string> Formal(global::app.goal.step.@this step)
    {
        var writer = new global::app.channel.serializer.formal.Writer();
        await step.Code.Output(writer, global::app.View.Store, global::PLang.Tests.TestApp.SharedContext);
        return writer.ToString();
    }

    [Test]
    public async Task EveryGoldenStep_WritesPythonsFormal_ByteForByte()
    {
        var goal = Goal();
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var written = await Formal(Step(goal, entry));
            var expected = entry.GetProperty("formal").GetString();
            if (written != expected)
                differ.Add($"{entry.GetProperty("goal").GetString()}[{entry.GetProperty("index").GetInt32()}]\n  expected: {expected}\n  written:  {written}");
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task Leaves_WriteAsFormalLiterals()
    {
        var writer = new global::app.channel.serializer.formal.Writer();
        writer.BeginArray(6);
        writer.String("a \"quoted\" \\ line\n"); writer.Long(10000); writer.Double(0.24); writer.Double(1); writer.Bool(true); writer.Null();
        writer.EndArray();
        await Assert.That(writer.ToString()).IsEqualTo("[\"a \\\"quoted\\\" \\\\ line\\n\", 10000, 0.24, 1, true, null]");
    }

    [Test]
    public async Task ADict_WritesItsKeysQuoted()
    {
        var writer = new global::app.channel.serializer.formal.Writer();
        writer.BeginObject(); writer.Name("id"); writer.String("x"); writer.Name("n"); writer.Long(5); writer.EndObject();
        await Assert.That(writer.ToString()).IsEqualTo("{\"id\": \"x\", \"n\": 5}");
    }
}
