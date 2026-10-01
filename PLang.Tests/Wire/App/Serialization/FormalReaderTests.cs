namespace PLang.Tests.App.Serialization;

// The formal reader (goal/step/action/serializer/Formal.cs) against the python reference: every golden step's
// formal parses and writes back byte for byte; its untyped form (as the LLM and a programmer write it) parses
// to the same actions; and every error python's parser answers, the C# parser answers the same — message,
// line and column (formal_errors.json, written by tools/decider/formal_fixture.py).
public class FormalReaderTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.data.@this Read(string formal, out global::app.goal.step.@this step)
    {
        var goal = FormalWriterTests.Goal(app.actor.list.User.Context);
        step = new global::app.goal.step.@this { Goal = goal };
        return new global::app.goal.step.action.formal.Reader(step, app.actor.list.User.Context.App.module.list).Read(formal, app.actor.list.User.Context);
    }

    private async Task<string> Written(global::app.data.@this read)
    {
        var writer = new global::app.goal.step.action.formal.Writer();
        await ((global::app.goal.step.action.list.@this)read.Peek()!).Output(writer, global::app.View.Store, app.actor.list.User.Context);
        return writer.ToString();
    }

    private async Task<string> RoundTrips(string key)
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

    // The clauses after the step's first action, in the order they stand in its code.
    private static string Clauses(global::app.data.@this read) =>
        string.Join(", ", ((global::app.goal.step.action.list.@this)read.Peek()!).Items().Skip(1)
            .Select(m => $"{m.Module.Name}.{m.Name}{(m["StatusCode"] is { } s ? $"({s.Value})" : "")}"));

    // a goal's name in an action slot is refused where it's read, naming the fix — never held as a value
    // nothing can read back
    [Test]
    public async Task AnActionSlot_GivenAName_IsRefused()
    {
        var read = Read("channel.set(Name=\"builder\", Goal=\"BuilderChannel\")", out _);

        await read.IsFailure();
        await Assert.That(read.Error!.Message).Contains("`Goal` takes an action");
        await Read("channel.set(Name=\"builder\", Goal=goal.call(Name=\"BuilderChannel\"))", out _).IsSuccess();
    }

    // An http header's name holds a hyphen: a dict's key written bare may hold one (the educator's `X-Probe: "x"`),
    // and it writes back quoted, which reads again byte for byte.
    [Test]
    public async Task ADictKeyWrittenBare_MayHoldAHyphen_AndWritesBackQuoted()
    {
        var read = Read("http.request(Url=\"https://httpbin.org/anything\", Header={X-Probe: \"x\", Authorization: \"Bearer 1\"})", out _);

        await read.IsSuccess();
        var written = await Written(read);
        await Assert.That(written).Contains("\"X-Probe\": \"x\"");
        await Assert.That(await Written(Read(written, out _))).IsEqualTo(written);
    }

    [Test]
    public async Task Clauses_AreTheActionsSiblings_InTheOrderWritten_AndWriteBackSo()
    {
        var read = Read("file.read(Path=\"a.txt\"); on.timeout(After=\"00:00:00.1000000\"); on.error(Recovery=[goal.call(Name=\"Fix\")]); on.cache(Duration=\"00:05:00\")", out _);

        await read.IsSuccess();
        var code = (global::app.goal.step.action.list.@this)read.Peek()!;
        await Assert.That(code.Items().Skip(1).All(a => a is global::app.goal.step.action.clause.@this)).IsTrue();
        await Assert.That(Clauses(read)).IsEqualTo("on.timeout, on.error, on.cache");
        var written = await Written(read);
        await Assert.That(written).Contains("; on.timeout(");
        await Assert.That(written).Contains("; on.error(Recovery: list<action> = [goal.call(");
        await Assert.That(written).Contains("; on.cache(");
    }

    // A duration is written as the type teaches it (ISO 8601) and reads to its length.
    [Test]
    public async Task AClausesDuration_WrittenIso_ReadsToItsLength()
    {
        var read = Read("file.read(Path=\"a.txt\"); on.timeout(After=\"PT0.1S\"); on.cache(Duration=\"PT5M\")", out _);

        await read.IsSuccess();
        var code = ((global::app.goal.step.action.list.@this)read.Peek()!).Items().ToList();
        var context = app.actor.list.User.Context;
        // a run reads the row as its Data — the raw slice lifts to the declared type there
        var after = await code[1]["After"]!.Data(context).Value();
        var duration = await code[2]["Duration"]!.Data(context).Value();
        await Assert.That((System.TimeSpan)(global::app.type.item.duration.@this)after!).IsEqualTo(System.TimeSpan.FromMilliseconds(100));
        await Assert.That((System.TimeSpan)(global::app.type.item.duration.@this)duration!).IsEqualTo(System.TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task AClause_LeadingTheStep_IsRefused()
    {
        var read = Read("on.error(Ignore=true)", out _);

        await read.IsFailure();
        await Assert.That(read.Error!.Message).Contains("is a clause of the action before it");
    }

    [Test]
    public async Task ABodyStep_IsWrittenOnItsParentsLine()
    {
        var goal = FormalWriterTests.Goal(app.actor.list.User.Context);
        var step = new global::app.goal.step.@this { Goal = goal, Line = new() { Number = 12, Indent = 1 } };

        var read = new global::app.goal.step.action.formal.Reader(step, app.actor.list.User.Context.App.module.list).Read(
            "condition.if(Left=%n%, Operator=\"<\", Right=5) { goal.return() }", app.actor.list.User.Context);

        await read.IsSuccess();
        var body = ((global::app.goal.step.action.list.@this)read.Peek()!).Items().Single().Child[0];
        await Assert.That(body.Line.Number).IsEqualTo(12);
        await Assert.That(body.Line.Indent).IsEqualTo(1);
    }

    [Test]
    public async Task TwoOnErrorClauses_KeepTheOrderWritten()
    {
        var read = Read("file.read(Path=\"a.txt\"); on.timeout(After=\"00:00:00.1000000\"); on.error(StatusCode=404, Recovery=[goal.call(Name=\"Missing\")]); on.error(Recovery=[goal.call(Name=\"Fix\")])", out _);

        await read.IsSuccess();
        await Assert.That(Clauses(read)).IsEqualTo("on.timeout, on.error(404), on.error");
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
    // (formal_bare.json) — and an open slot's list of dicts with bare keys reads as that list; a symbol
    // that is not an option is in formal_errors.json.
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
        await Assert.That(cases.Count).IsEqualTo(8);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // An empty `{ }` is syntax that reads; the rule it breaks is the chain check's, which says where
    // the body goes for this step (nothing is indented below it: inside the if's `{ }`).
    [Test]
    public async Task AnEmptyBody_Reads_AndTheChainCheckRefusesIt_SayingWhereTheBodyGoes()
    {
        var read = Read("condition.if(Left=%a%, Operator=\"isempty\") { }; condition.else() { output.write(Data=\"x\") }", out var step);
        await read.IsSuccess();
        foreach (var a in ((global::app.goal.step.action.list.@this)read.Peek()!).Items()) step.Code.Add(a);

        var invalid = await step.Code.Validate(app.actor.list.User.Context);

        var body = invalid!.list?.FirstOrDefault(e => e.Key == "BodyMissing") ?? invalid;
        await Assert.That(body.Key).IsEqualTo("BodyMissing");
        await Assert.That(body.Message).Contains("the step has nothing indented below it, so what the step does when the condition holds goes inside the if's `{ }`");
    }

    [Test]
    public async Task AnError_IsReturned_KeyedFormalInvalid_WithTheFix()
    {
        var read = Read("file.read(Pth=\"x\")", out _);
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("FormalInvalid");
        await Assert.That(read.Error.FixSuggestion).IsEqualTo("`file.read` has no property `Pth` (it has Path, Template)");
    }

    [Test]
    public async Task EveryAction_IsBornHoldingTheStep_ClausesKeepTheOrderWritten()
    {
        // two on.error clauses: B is written first, so B is asked first
        var read = Read("goal.call(Name=\"X\"); on.error(Key=\"B\", Recovery=[goal.call(Name=\"RB\")]); on.error(Key=\"A\", Recovery=[goal.call(Name=\"RA\")])", out var step);
        var actions = ((global::app.goal.step.action.list.@this)read.Peek()!).Items().ToList();
        var call = actions[0];
        await Assert.That(call.Name).IsEqualTo("call");
        await Assert.That(call.Step).IsSameReferenceAs(step);
        var clauses = actions.Skip(1).ToList();
        // a row's value is held raw, as its slice ("A"), until a run reads it
        await Assert.That(string.Join(",", clauses.Select(m => m.Property["Key"]!.Value!.ToString()))).IsEqualTo("\"B\",\"A\"");
        await Assert.That(clauses[0].Step).IsSameReferenceAs(step);
        var recovery = (global::app.goal.step.action.list.@this)clauses[0]["Recovery"]!.Value!;
        await Assert.That(recovery.Items().Single().Step).IsSameReferenceAs(step);
    }

    [Test]
    public async Task AFrozenDefault_IsTheActionsDefault_WrittenBackWithQuestionEquals()
    {
        var read = Read("goal.return(Depth: number ?= 1)", out _);
        await Assert.That(read.Success).IsTrue();
        await Assert.That(await Written(read)).IsEqualTo("goal.return(Depth: number ?= 1)");
    }
}
