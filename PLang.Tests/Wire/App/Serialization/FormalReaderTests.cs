namespace PLang.Tests.App.Serialization;

// The formal reader (goal/step/action/formal/reader.cs) against pinned fixtures: every golden step's formal parses
// and writes back byte for byte; its untyped form (as the LLM and a programmer write it) parses to the same actions;
// and every pinned error is the reader's own — message, line and column (formal_errors.json). An intended change
// re-pins them through the Accept tests.
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
            .Select(m => $"{m.Module.Name}.{m.Name}{(m["Status"] is { } s ? $"({s.Value})" : "")}"));

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

    // a list of dicts is a value for a list option (llm.query's messages); only goal.call's parameters, which are
    // argument rows, are refused written as a list
    [Test]
    public async Task AListOfDicts_ReadsAsAValue_ForAListOption()
    {
        var read = Read("llm.query(Message=[{Role: \"system\", Content: \"be brief\"}, {Role: \"user\", Content: \"hi\"}])", out _);

        await read.IsSuccess();
    }

    // `continue the conversation` as FixSteps writes it: the formal reads, rides its .pr (an object template), and
    // opens as the conversation
    [Test]
    public async Task AConversationContinuing_ReadsRidesThePr_AndOpens()
    {
        var conversation = await Continuing("{continue: %answer%}");

        await Assert.That(await conversation.Value()).IsTypeOf<global::app.module.llm.type.conversation.@this>();
        await conversation.IsSuccess();
    }

    // an event written as a value (a goal's path where the event's path goes) is refused saying how an event is
    // reached — never a missing reader thrown at whoever reads the .pr back
    [Test]
    public async Task AnEventWrittenAsAValue_IsRefused_SayingItsPath()
    {
        var read = Read("on.event(Event=\"/events/Runtime/DebugErrorInIde\", When=after, Action=goal.call(Name=\"Y\"))", out _);
        var ctx = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("on", ((global::app.goal.step.action.list.@this)read.Peek()!)[0])));

        var ev = goal.Step[0].Code[0].Property["Event"]!.Data(ctx);
        await ev.Value();

        await ev.IsFailure();
        await Assert.That(ev.Error!.Key).IsEqualTo("NotAnEvent");
        // the paths it teaches are real events, before/after in When — the retry reads this
        await Assert.That(ev.Error.Message).Contains("Event=%!app.type.step.on.start%, When=before");
        await Assert.That(ev.Error.Message).DoesNotContain("on.before");
    }

    // an event reached by its path, read back through a real .pr, binds: the slot is the event the path names
    [Test]
    public async Task AnEventByItsPath_ReadsThePr_AndBinds()
    {
        var read = Read("on.event(Event=%!app.type.path.on.create%, When=after, Action=goal.call(Name=\"Y\"))", out _);
        var ctx = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("on", ((global::app.goal.step.action.list.@this)read.Peek()!)[0])));

        var bound = await goal.Step[0].Code[0].Start(ctx);

        await bound.IsSuccess();
        await Assert.That(await bound.Value()).IsTypeOf<global::app.@event.binding.action.@this>();
    }

    // a value made from a dict of literal members keeps them through a real .pr load
    [Test]
    public async Task ALimitsLiteralMembers_RideThePr()
    {
        var limit = await Slot("llm.query(Message=[{Role: \"user\", Content: \"x\"}], Limit={token: 5})", "Limit");

        var opened = await limit.Value();

        await limit.IsSuccess();
        await Assert.That(((global::app.module.llm.type.limit.@this)opened!).Token.ToString()).IsEqualTo("5");
    }

    [Test]
    public async Task AQuerysLiteralMembers_RideThePr()
    {
        var query = await Slot("list.query(List=%users%, Query={group: \"name\"})", "Query");

        var opened = await query.Value();

        await query.IsSuccess();
        await Assert.That(opened).IsAssignableTo<global::app.module.list.type.query.@this>();
        var writer = new global::app.goal.step.action.formal.Writer();
        await opened!.Output(writer, global::app.View.Store, app.actor.list.User.Context);
        await Assert.That(writer.ToString()).Contains("group").And.Contains("name");
    }

    // a permission written as its dict, read back through a real .pr and then as a permission, keeps its members
    [Test]
    public async Task APermissionsLiteralMembers_RideThePr()
    {
        var value = await Slot("variable.set(Name=%p%, Value={actor: \"u\", path: \"/granted\", verbs: [\"write\"]})", "Value");

        var grant = await global::app.data.@this<global::app.type.item.permission.@this>.From(value).Value();

        await Assert.That(grant!.Path.ToString()).IsEqualTo("/granted");
        await Assert.That(grant.Verbs.Count).IsEqualTo(1);
    }

    // as a list of them, each row taken as a permission
    [Test]
    public async Task PermissionsLiteralMembers_RideThePr_AsAList()
    {
        var value = await Slot("variable.set(Name=%p%, Value=[{actor: \"u\", path: \"/granted\", verbs: [\"write\"]}])", "Value");

        var grants = await global::app.data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>.From(value).Value();
        var grant = await grants!.Rows(app.actor.list.User.Context).Single().Value<global::app.type.item.permission.@this>();

        await Assert.That(grant!.Path.ToString()).IsEqualTo("/granted");
        await Assert.That(grant.Verbs.Count).IsEqualTo(1);
    }

    // made from CLR values (as a C# test makes a step), written as a .pr and read back
    [Test]
    [Skip("a .pr written from CLR values holds each dict member as a whole Data row, read back unread; permission.Create type-tests the held value (Get<Text>), so the path comes back empty — waits on how a Create opens its members")]
    public async Task PermissionsMadeFromClr_RideThePr_AsAList()
    {
        var ctx = app.actor.list.User.Context;
        var may = new List<object?> { new Dictionary<string, object?> { ["path"] = "/granted", ["verbs"] = new List<object?> { "write" } } };
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("slot", global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "p", "variable"), ("Value", may)))));
        var value = goal.Step[0].Code[0].Property["Value"]!.Data(ctx);

        var grants = await global::app.data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>.From(value).Value();
        var grant = await grants!.Rows(ctx).Single().Value<global::app.type.item.permission.@this>();

        await Assert.That(grant!.Path.ToString()).IsEqualTo("/granted");
        await Assert.That(grant.Verbs.Count).IsEqualTo(1);
    }

    [Test]
    [Skip("a .pr written from CLR values holds each dict member as a whole Data row, read back unread; limit.Create type-tests the held value (Peek() is number) and refuses 'token' — waits on how a Create opens its members")]
    public async Task ALimitMadeFromClr_RidesThePr()
    {
        var ctx = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("slot", global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "p", "variable"), ("Value", new Dictionary<string, object?> { ["token"] = 5 })))));
        var value = goal.Step[0].Code[0].Property["Value"]!.Data(ctx);

        var limit = global::app.data.@this<global::app.module.llm.type.limit.@this>.From(value);
        var opened = await limit.Value();

        await limit.IsSuccess();
        await Assert.That(opened!.Token.ToString()).IsEqualTo("5");
    }

    // variable.set's Type is a type: the .pr row is {name: Type, type: {name: type}, value: {name: path}}
    [Test]
    public async Task ASetsType_IsWrittenAsAType()
    {
        var ctx = app.actor.list.User.Context;
        async Task<string> Row(string type)
        {
            var read = Read($"variable.set(Name=%p%, Value=\"a.txt\", Type={type})", out _);
            await read.IsSuccess();
            var goal = global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
                global::PLang.Tests.Shared.Make.Step("set", ((global::app.goal.step.action.list.@this)read.Peek()!)[0]));
            var pr = System.Text.Json.Nodes.JsonNode.Parse(await ctx.Pr(goal))!;
            return pr["step"]![0]!["code"]![0]!["property"]!.AsArray().Single(p => (string?)p!["name"] == "Type")!.ToJsonString();
        }

        await Assert.That(await Row("{name: \"path\"}")).IsEqualTo("{\"name\":\"Type\",\"type\":{\"name\":\"type\"},\"value\":{\"name\":\"path\"}}");
        await Assert.That(await Row("\"path\"")).IsEqualTo("{\"name\":\"Type\",\"type\":{\"name\":\"type\"},\"value\":\"path\"}");
    }

    // a .pr built before Type was a type holds it as a dict or a text: it still sets the value as that type
    [Test]
    [Arguments("{\"name\":\"dict\"}", "{\"name\":\"path\"}")]
    [Arguments("{\"name\":\"text\"}", "\"path\"")]
    public async Task ASetsTypeBuiltAsADictOrAText_StillSetsThatType(string rowType, string rowValue)
    {
        var ctx = app.actor.list.User.Context;
        var read = Read("variable.set(Name=%p%, Value=\"a.txt\", Type=\"path\")", out _);
        var built = global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("set", ((global::app.goal.step.action.list.@this)read.Peek()!)[0]));
        var pr = (await ctx.Pr(built)).Replace(
            "\"type\": {\n                \"name\": \"type\"\n              },\n              \"value\": \"path\"",
            $"\"type\": {rowType},\n              \"value\": {rowValue}");
        await Assert.That(pr).Contains(rowType);
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.Read(app, pr);

        await (await goal.Step[0].Code[0].Start(ctx)).IsSuccess();

        await Assert.That(await (await ctx.Variable.Get("p")).Value()).IsAssignableTo<global::app.type.item.path.@this>();
    }

    // a list slot given one value the build reads (a module, by its %!…% path) walks: the value is a list of one,
    // never a cast the build dies on
    [Test]
    public async Task AListSlotGivenAModule_Walks_NeverACast()
    {
        var ctx = app.actor.list.User.Context;
        var read = Read("goal.call(Name=\"Page\", Parameter=%!app.module.file%)", out _);
        await read.IsSuccess();
        var action = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        var goal = global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal", global::PLang.Tests.Shared.Make.Step("call", action));

        var declined = await goal.Step[0].Scope(ctx);

        await Assert.That(declined).IsEmpty();
    }

    // a goal call given one value where its named rows go is refused at build, saying the form — never bound as
    // nothing
    [Test]
    public async Task AGoalCallGivenAModuleForItsParameters_IsRefused_SayingTheForm()
    {
        var ctx = app.actor.list.User.Context;
        var read = Read("goal.call(Name=\"Page\", Parameter=%!app.module.file%)", out _);
        var action = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal", global::PLang.Tests.Shared.Make.Step("call", action));

        var refused = await action.Validate(ctx);

        await Assert.That(refused?.Message).Contains("Parameter takes named rows: {name: %!app.module.file%}");
    }

    [Test]
    [Arguments("{module: %!app.module.file%}")]
    [Arguments("{}")]
    public async Task AGoalCallsNamedRows_OrNone_Pass(string parameter)
    {
        var ctx = app.actor.list.User.Context;
        var read = Read($"goal.call(Name=\"Page\", Parameter={parameter})", out _);
        var action = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal", global::PLang.Tests.Shared.Make.Step("call", action));

        await Assert.That(await action.Validate(ctx)).IsNull();
    }

    // a property of the one action a formal line reads, through a real .pr load
    private async Task<global::app.data.@this> Slot(string formal, string property)
    {
        var read = Read(formal, out _);
        await read.IsSuccess();
        var ctx = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("slot", ((global::app.goal.step.action.list.@this)read.Peek()!)[0])));
        return goal.Step[0].Code[0].Property[property]!.Data(ctx);
    }

    // a conversation writes what it continues as written — the reference, never what it names now (unset at build)
    [Test]
    public async Task AConversation_WritesItsReferenceAsWritten()
    {
        var formal = "llm.query(Message=[{Role: \"user\", Content: \"again\"}], Conversation={continue: %answer%})";

        var written = await Written(Read(formal, out _));

        await Assert.That(written).Contains("%answer%");
        await Assert.That(written).DoesNotContain("null");
    }

    // a conversation written as text reads as the value it continues — never a reader's exception; that a text is no
    // llm answer is the query's to say (QueryConversationTests)
    [Test]
    public async Task AConversationWrittenAsText_ReadsAsTheValueItContinues()
    {
        var conversation = await Continuing("\"{continue: %answer%}\"");

        var opened = await conversation.Value();

        await conversation.IsSuccess();
        await Assert.That(opened).IsTypeOf<global::app.module.llm.type.conversation.@this>();
        await Assert.That(((global::app.module.llm.type.conversation.@this)opened!).Continue).IsNotNull();
    }

    // llm.query's Conversation as written, through a real .pr load, with %answer% set
    private async Task<global::app.data.@this> Continuing(string written)
    {
        var read = Read("llm.query(Message=[{Role: \"user\", Content: \"again\"}], Conversation=" + written + ")", out _);
        await read.IsSuccess();
        var ctx = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("continue", ((global::app.goal.step.action.list.@this)read.Peek()!)[0])));
        await ctx.Variable.Set("answer", "earlier");
        return goal.Step[0].Code[0].Property["Conversation"]!.Data(ctx);
    }

    [Test]
    public async Task GoalCallsParameters_WrittenAsAListOfDicts_AreRefused()
    {
        var read = Read("goal.call(Name=\"X\", Parameter=[{kind: \"a\"}])", out _);

        await read.IsFailure();
        await Assert.That(read.Error!.Message).Contains("arguments are written as one dict");
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
        var read = Read("file.read(Path=\"a.txt\"); on.timeout(After=\"00:00:00.1000000\"); on.error(Status=404, Recovery=[goal.call(Name=\"Missing\")]); on.error(Recovery=[goal.call(Name=\"Fix\")])", out _);

        await read.IsSuccess();
        await Assert.That(Clauses(read)).IsEqualTo("on.timeout, on.error(404), on.error");
    }

    [Test]
    public async Task EveryGoldenStep_Typed_ParsesAndWritesBack_ByteForByte()
        => await Assert.That(await RoundTrips("formal")).IsEqualTo("");

    [Test]
    public async Task EveryGoldenStep_Untyped_ParsesToTheSameActions()
        => await Assert.That(await RoundTrips("untyped")).IsEqualTo("");

    private const string ErrorsPinned = "PLang.Tests/Wire/App/Serialization/formal_errors.json";
    private const string BarePinned = "PLang.Tests/Wire/App/Serialization/formal_bare.json";

    private static List<System.Text.Json.JsonElement> Cases(string pinned)
        => System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), pinned)))
            .RootElement.EnumerateArray().ToList();

    // What the reader answers for an input that must not read: its message (null when it reads).
    private string? Refusal(string input) => Read(input, out _) is { Success: false } read ? read.Error!.Message : null;

    // What the reader makes of an input written bare: the formal it writes back, or why it refuses.
    private async Task<string> Reread(string input)
        => Read(input, out _) is { Success: true } read ? await Written(read) : Read(input, out _).Error!.Message;

    // Every pinned error (formal_errors.json) is the reader's message, line and column for its input.
    [Test]
    public async Task EveryPinnedError_IsTheReadersMessageLineAndColumn()
    {
        var differ = new List<string>();
        foreach (var c in Cases(ErrorsPinned))
        {
            var input = c.GetProperty("input").GetString()!;
            var expected = c.GetProperty("message").GetString();
            var message = Refusal(input);
            if (message != expected) differ.Add($"{input.Replace("\n", "\\n")}\n  pinned: {expected}\n  c#:     {message}");
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // A choice's symbol option written bare (Operator=>=) reads as that option (formal_bare.json) — and an open
    // slot's list of dicts with bare keys reads as that list; a symbol that is not an option is in formal_errors.json.
    [Test]
    public async Task EveryBareSymbolOption_ReadsAsThePinnedOption()
    {
        var cases = Cases(BarePinned);
        var differ = new List<string>();
        foreach (var c in cases)
        {
            var input = c.GetProperty("input").GetString()!;
            var written = await Reread(input);
            if (written != c.GetProperty("formal").GetString()) differ.Add($"{input}\n  pinned: {c.GetProperty("formal").GetString()}\n  c#:     {written}");
        }
        await Assert.That(cases.Count).IsEqualTo(8);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // Re-pins formal_errors.json's messages and formal_bare.json's formal from C#: run by hand after an intended change
    // to the reader, then review the diff.
    [Test, Explicit]
    public async Task AcceptTheFixtures()
    {
        var errors = Fixture.Read(ErrorsPinned).AsArray();
        var errorCases = Cases(ErrorsPinned);
        for (int i = 0; i < errorCases.Count; i++) errors[i]!["message"] = Refusal(errorCases[i].GetProperty("input").GetString()!);
        Fixture.Write(ErrorsPinned, errors);

        var bare = Fixture.Read(BarePinned).AsArray();
        var bareCases = Cases(BarePinned);
        for (int i = 0; i < bareCases.Count; i++) bare[i]!["formal"] = await Reread(bareCases[i].GetProperty("input").GetString()!);
        Fixture.Write(BarePinned, bare);
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
