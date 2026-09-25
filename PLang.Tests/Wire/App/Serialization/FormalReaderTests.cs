namespace PLang.Tests.App.Serialization;

// The formal reader (goal/step/action/serializer/Formal.cs) against the python reference: every golden step's
// formal parses and writes back byte for byte; its untyped form (as the LLM and a programmer write it) parses
// to the same actions; and every error python's parser answers, the C# parser answers the same — message,
// line and column (formal_errors.json, written by tools/decider/formal_fixture.py).
public class FormalReaderTests
{
    private static global::app.data.@this Read(string formal, out global::app.goal.step.@this step)
    {
        var goal = FormalWriterTests.Goal();
        step = new global::app.goal.step.@this { Goal = goal };
        return new global::app.goal.step.action.serializer.Formal(step).Read(formal, global::PLang.Tests.TestApp.SharedContext);
    }

    private static async Task<string> Written(global::app.data.@this read)
    {
        var writer = new global::app.channel.serializer.formal.Writer();
        await ((global::app.goal.step.action.list.@this)read.Peek()!).Output(writer, global::app.View.Store, global::PLang.Tests.TestApp.SharedContext);
        return writer.ToString();
    }

    private static async Task<string> RoundTrips(string key)
    {
        var differ = new List<string>();
        foreach (var entry in FormalWriterTests.Golden())
        {
            var input = entry.GetProperty(key).GetString()!;
            var expected = entry.GetProperty("formal").GetString();
            var read = Read(input, out _);
            var at = $"{entry.GetProperty("goal").GetString()}[{entry.GetProperty("index").GetInt32()}]";
            if (!read.Success) { differ.Add($"{at} does not parse: {read.Error!.Message}\n  {input}"); continue; }
            var written = await Written(read);
            if (written != expected) differ.Add($"{at}\n  expected: {expected}\n  written:  {written}");
        }
        return string.Join("\n", differ);
    }

    [Test]
    public async Task EveryGoldenStep_Typed_ParsesAndWritesBack_ByteForByte()
        => await Assert.That(await RoundTrips("formal")).IsEqualTo("");

    [Test]
    public async Task EveryGoldenStep_Untyped_ParsesToTheSameActions()
        => await Assert.That(await RoundTrips("untyped")).IsEqualTo("");

    [Test]
    public async Task EveryPythonError_HasItsTwin_SameMessageLineAndColumn()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_errors.json")))
            dir = dir.Parent;
        var cases = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_errors.json"))).RootElement.EnumerateArray();
        var differ = new List<string>();
        foreach (var c in cases)
        {
            var input = c.GetProperty("input").GetString()!;
            var expected = c.GetProperty("message").GetString();
            var read = Read(input, out _);
            var message = read.Success ? null : read.Error!.Message;
            if (message != expected) differ.Add($"{input.Replace("\n", "\\n")}\n  python: {expected}\n  c#:     {message}");
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // A choice's symbol option written bare (Operator=>=) reads as that option, as python reads it
    // (formal_bare.json); a symbol that is not an option is in formal_errors.json.
    [Test]
    public async Task EveryBareSymbolOption_ReadsAsTheOption_AsPythonReadsIt()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_bare.json")))
            dir = dir.Parent;
        var cases = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Serialization", "formal_bare.json"))).RootElement.EnumerateArray().ToList();
        var differ = new List<string>();
        foreach (var c in cases)
        {
            var input = c.GetProperty("input").GetString()!;
            var read = Read(input, out _);
            var written = read.Success ? await Written(read) : read.Error!.Message;
            if (written != c.GetProperty("formal").GetString()) differ.Add($"{input}\n  python: {c.GetProperty("formal").GetString()}\n  c#:     {written}");
        }
        await Assert.That(cases.Count).IsEqualTo(6);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task AnError_IsReturned_KeyedFormalInvalid_WithTheFix()
    {
        var read = Read("file.read(Pth=\"x\")", out _);
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("FormalInvalid");
        await Assert.That(read.Error.FixSuggestion).IsEqualTo("`file.read` has no property `Pth` (it has Path, ResolveVariables)");
    }

    [Test]
    public async Task EveryAction_IsBornHoldingTheStep_AModifierWrapsOutermostFirst()
    {
        var read = Read("error.handle(Key=\"A\", Recovery=[goal.call(Name=\"RA\")]) { error.handle(Key=\"B\", Recovery=[goal.call(Name=\"RB\")]) { goal.call(Name=\"X\") } }", out var step);
        var actions = (global::app.goal.step.action.list.@this)read.Peek()!;
        var call = actions.Items().Single();
        await Assert.That(call.Name).IsEqualTo("call");
        await Assert.That(call.Step).IsSameReferenceAs(step);
        // a row's value is held raw, as its slice ("A"), until a run reads it
        await Assert.That(string.Join(",", call.Modifier.Select(m => m.Property["Key"]!.Value!.ToString()))).IsEqualTo("\"A\",\"B\"");
        await Assert.That(call.Modifier[0].Recovery.Items().Single().Step).IsSameReferenceAs(step);
    }

    [Test]
    public async Task AFrozenDefault_IsTheActionsDefault_WrittenBackWithQuestionEquals()
    {
        var read = Read("goal.return(Depth: number ?= 1)", out _);
        await Assert.That(read.Success).IsTrue();
        await Assert.That(await Written(read)).IsEqualTo("goal.return(Depth: number ?= 1)");
    }
}
