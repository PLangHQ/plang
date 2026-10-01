namespace PLang.Tests.App.Serialization;

// The formal writer against a pinned fixture (formal_golden.json): each golden step's .pr rows, read through the
// real step reader, written through formal.Writer, are the pinned formal byte for byte. An intended change re-pins
// it through AcceptTheFixture.
public class FormalWriterTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    internal static System.Text.Json.JsonElement[] Golden()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_golden.json")))
            dir = dir.Parent;
        var path = System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_golden.json");
        return System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement.EnumerateArray().ToArray();
    }

    internal static global::app.goal.@this Goal(global::app.actor.context.@this context)
    {
        var path = global::app.type.item.path.@this.Resolve("/formal.goal", context);
        return global::app.goal.@this.Parse("Formal\n- a step\n", path, context)!;
    }

    // The step as the .pr holds it: {index, text, code: [rows]} — read through the step reader, as the goal loader
    // reads the build's own bytes (granted: its templates hold every variable they list).
    internal static global::app.goal.step.@this Step(global::app.goal.@this goal, System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var json = "{\"index\":" + entry.GetProperty("index").GetInt32()
                   + ",\"text\":" + System.Text.Json.JsonSerializer.Serialize(entry.GetProperty("text").GetString())
                   + ",\"code\":" + entry.GetProperty("pr").GetRawText() + "}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        return (global::app.goal.step.@this)new global::app.goal.step.serializer.Reader(goal)
            .Read(ref reader, null, new global::app.type.reader.ReadContext(context, "plang", IsBuilt: true));
    }

    internal static async Task<string> Formal(global::app.goal.step.@this step, global::app.actor.context.@this context)
    {
        var writer = new global::app.goal.step.action.formal.Writer();
        await step.Code.Output(writer, global::app.View.Store, context);
        return writer.ToString();
    }

    // Re-pins formal_golden.json's formal from C#: run by hand after an intended change to the formal writer, then
    // review the diff.
    [Test, Explicit]
    public async Task AcceptTheFixture()
    {
        var context = app.actor.list.User.Context;
        var goal = Goal(context);
        const string pinnedPath = "PLang.Tests/Wire/App/Serialization/formal_golden.json";
        var pinned = Fixture.Read(pinnedPath).AsArray();
        var entries = Golden();
        for (int i = 0; i < entries.Length; i++) pinned[i]!["formal"] = await Formal(Step(goal, entries[i], context), context);
        Fixture.Write(pinnedPath, pinned);
    }

    [Test]
    public async Task EveryGoldenStep_WritesThePinnedFormal_ByteForByte()
    {
        var context = app.actor.list.User.Context;
        var goal = Goal(context);
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var written = await Formal(Step(goal, entry, context), context);
            var expected = entry.GetProperty("formal").GetString();
            if (written != expected)
                differ.Add($"{entry.GetProperty("goal").GetString()}[{entry.GetProperty("index").GetInt32()}]\n  expected: {expected}\n  written:  {written}");
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task Leaves_WriteAsFormalLiterals()
    {
        var writer = new global::app.goal.step.action.formal.Writer();
        writer.BeginArray(6);
        writer.String("a \"quoted\" \\ line\n"); writer.Long(10000); writer.Double(0.24); writer.Double(1); writer.Bool(true); writer.Null();
        writer.EndArray();
        await Assert.That(writer.ToString()).IsEqualTo("[\"a \\\"quoted\\\" \\\\ line\\n\", 10000, 0.24, 1, true, null]");
    }

    [Test]
    public async Task ADict_WritesItsKeysQuoted()
    {
        var writer = new global::app.goal.step.action.formal.Writer();
        writer.BeginObject(); writer.Name("id"); writer.String("x"); writer.Name("n"); writer.Long(5); writer.EndObject();
        await Assert.That(writer.ToString()).IsEqualTo("{\"id\": \"x\", \"n\": 5}");
    }
}
