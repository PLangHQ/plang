namespace PLang.Tests.App;

// `%!app.x%` reads app's member x as the asker sees it: the asker's call stack, trace, last answer.
public class AppNodesTests
{
    private static global::app.@this NewApp() => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-nodes-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

    private static System.Threading.Tasks.ValueTask<global::app.data.@this> Read(string variable, global::app.actor.context.@this context)
        => new global::app.type.item.variable.@this(variable.Trim('%')).Start(context);

    [Test]
    public async Task Callstack_IsTheAskersCallStack()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;

        var read = await Read("%!app.call%", user);

        await Assert.That(read.IsInitialized).IsTrue();
        await Assert.That(ReferenceEquals(read.Peek(), user.call)).IsTrue();
    }

    // Every way a program reads the call stack still reads through, the stack being a plang value.
    [Test]
    public async Task Callstack_ProgramReads_StillReadThrough()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var goal = new global::app.goal.@this(app.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", user) };
        await using var frame = user.call.Push(goal);
        frame.Tag(new global::app.type.item.dict.@this(new System.Collections.Generic.Dictionary<string, object?> { ["owner"] = "ingi" }, user.App.type.list[typeof(global::app.type.item.dict.@this)]), user);
        frame.Record(new global::app.error.@this("seen", "Seen", 400), user);

        await Assert.That((await (await Read("%!call.Current.Depth%", user)).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await Read("%!call.Scope%", user)).Peek()).IsSameReferenceAs(frame);
        await Assert.That((await (await Read("%!call.Scope.Tags.owner%", user)).Value())?.ToString()).IsEqualTo("ingi");
        await Assert.That((await (await Read("%!call.Audit.Count%", user)).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await Read("%!call.Audit%", user)).IsInitialized).IsTrue();
        await Assert.That((await (await Read("%!call.Audit[0].Message%", user)).Value())?.ToString()).IsEqualTo("seen");
        await Assert.That((await (await Read("%!call.Current.Errors.Count%", user)).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await Read("%!call.Current.Diffs%", user)).Success).IsTrue();
        await Assert.That((await Read("%!call%", user)).Peek()).IsSameReferenceAs(user.call);
    }

    // The call stack is a plang value: written, it shows its frame in play and the run's errors — never the
    // variables or parameters an error keeps; dumped, it completes.
    [Test]
    public async Task Callstack_IsAPlangValue_WrittenWithoutVariables_AndDumped()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var goal = new global::app.goal.@this(app.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", user) };
        await user.Variable.Set("secret", user.Ok("s3cr3t-value"));
        await using var frame = user.call.Push(goal);
        var error = new global::app.error.@this("it failed here", "Boom", 400) { Variables = user.Variable.Snapshot() };
        frame.Record(error, user);

        using var ms = new System.IO.MemoryStream();
        var written = await user.Format("application/json").Encode(ms, user.Ok(user.call), user);
        await written.IsSuccess();
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).Contains(frame.Id.ToString()!);
        await Assert.That(json).Contains("it failed here");
        await Assert.That(json).DoesNotContain("s3cr3t-value");
        await Assert.That(json.ToLowerInvariant()).DoesNotContain("\"variables\"");
        await Assert.That(json.ToLowerInvariant()).DoesNotContain("\"params\"");

        var dumped = await user.call.Debug(user);
        await Assert.That(dumped).Contains(frame.Id.ToString()!);
    }

    // The call stack writes one flat form, the same in every view.
    [Test]
    public async Task Callstack_WritesOneForm_InEveryView()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var goal = new global::app.goal.@this(app.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", user) };
        await using var frame = user.call.Push(goal);
        frame.Record(new global::app.error.@this("seen", "Seen", 400), user);

        async Task<string> In(global::app.View view)
        {
            using var ms = new System.IO.MemoryStream();
            await using (var utf8 = new System.Text.Json.Utf8JsonWriter(ms))
            {
                var json = new global::app.type.item.kind.json.Writer(utf8, user.App.type.list);
                await json.Kind.pair.Output(user.call, json, view, user);
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        await Assert.That(await In(global::app.View.Debug)).IsEqualTo(await In(global::app.View.Out));
    }

    [Test]
    public async Task Trace_IsTheAskersTrace()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;

        var read = await Read("%!app.trace%", user);

        await Assert.That(ReferenceEquals(read.Peek(), user.Trace)).IsTrue();
    }

    // The trace is a plang value: its id reads through both spellings, and it writes and dumps as its id and start.
    [Test]
    public async Task Trace_IsAPlangValue_ReadWrittenAndDumped()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var id = user.Trace.Id.ToString();

        await Assert.That((await (await Read("%!trace.id%", user)).Value())?.ToString()).IsEqualTo(id);
        await Assert.That((await (await Read("%!app.trace.id%", user)).Value())?.ToString()).IsEqualTo(id);

        using var ms = new System.IO.MemoryStream();
        var written = await user.Format("application/json").Encode(ms, user.Ok(user.Trace), user);
        await written.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(ms.ToArray())).Contains(id);
        await Assert.That(await user.Trace.Debug(user)).Contains(id);
    }

    [Test]
    public async Task Data_IsWhatTheAskersLastActionAnswered()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        await user.Variable.Set("!data", user.Ok("answered"));

        var read = await Read("%!app.data%", user);

        await Assert.That((await read.Value())?.ToString()).IsEqualTo("answered");
    }

    // The app is a plang value a debug view dumps (the builder's `build.load` answers it into %app%, and --debug
    // shows each step's variables). Its nodes are navigated, never walked: the asker's memory holds the app, and a
    // dump that walked app.variable would walk the app again, without end.
    [Test]
    public async Task App_HeldInTheAskersMemory_IsDumped()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        await user.Variable.Set("app", user.Ok(app));

        var dumped = await app.Debug(user);

        await Assert.That(dumped).Contains(app.Id);
        await Assert.That((await Read("%!app.variable%", user)).IsInitialized).IsTrue();
    }

    // An actor written alone is its name, in every view: on the wire and in a dump.
    [Test]
    public async Task Actor_WrittenAlone_IsItsName()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        using var ms = new System.IO.MemoryStream();

        await (await user.Format("application/json").Encode(ms, user.Ok(app.actor.list.User), user)).IsSuccess();

        await Assert.That(System.Text.Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo($"\"{app.actor.list.User.Name}\"");
        await Assert.That(await app.actor.list.User.Debug(user)).IsEqualTo($"\"{app.actor.list.User.Name}\"");
    }

    // A parent written alone says what it knows: null when nothing links it, and then it reads false.
    [Test]
    public async Task Parent_Unlinked_IsNull_AndFalse()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        using var ms = new System.IO.MemoryStream();

        await (await user.Format("application/json").Encode(ms, user.Ok(app.parent), user)).IsSuccess();

        await Assert.That(System.Text.Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("null");
        await Assert.That(app.parent.IsTruthy()).IsFalse();
    }

    // The memory's system variables hold the app's types: %Now% the app's datetime, %GUID% the app's guid.
    [Test]
    public async Task SystemVariables_HoldTheAppsTypes()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;

        var now = await user.Variable.Get("Now");
        var guid = await user.Variable.Get("GUID");

        await Assert.That(now.Type).IsSameReferenceAs(app.type.list[typeof(global::app.type.item.datetime.@this)]);
        await Assert.That(now.Peek().Type).IsSameReferenceAs(app.type.list[typeof(global::app.type.item.datetime.@this)]);
        await Assert.That(guid.Type).IsSameReferenceAs(app.type.list[typeof(global::app.type.item.guid.@this)]);
    }

    // The app's members by their written names: what it writes at its top, in a view.
    private static System.Collections.Generic.List<string> Members(string written)
        => System.Text.Json.JsonDocument.Parse(written).RootElement.EnumerateObject().Select(p => p.Name).ToList();

    private static readonly string[] Shown = ["goal", "type", "actor", "error", "test", "shortcut", "module", "store",
        "parent", "id", "created", "updated"];
    private static readonly string[] Plumbing = ["cache", "fileSystem", "code", "debug", "build", "keepAlive", "absolutePath",
        "osAbsolutePath", "startedAt", "uptime"];

    // `write out %!app%`: on the wire the app writes what it holds — never the asker's memory, never its C# plumbing.
    [Test]
    public async Task App_OnTheWire_WritesWhatItHolds_NotTheMemoryNorThePlumbing()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        await user.Variable.Set("app", user.Ok(app));
        using var ms = new System.IO.MemoryStream();

        await (await user.Format("application/json").Encode(ms, user.Ok(app), user)).IsSuccess();

        var members = Members(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        foreach (var shown in Shown) await Assert.That(members).Contains(shown);
        await Assert.That(members).DoesNotContain("variable");
        foreach (var plumbing in Plumbing) await Assert.That(members).DoesNotContain(plumbing);
    }

    // A dump of the app shows the asker's memory too, the app inside it as its name, and no plumbing.
    [Test]
    public async Task App_InADump_ShowsTheMemory_WithTheAppAsItsName()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        await user.Variable.Set("app", user.Ok(app));

        var dumped = await app.Debug(user);

        var members = Members(dumped);
        foreach (var shown in Shown) await Assert.That(members).Contains(shown);
        await Assert.That(members).Contains("variable");
        foreach (var plumbing in Plumbing) await Assert.That(members).DoesNotContain(plumbing);
        var memory = System.Text.Json.JsonDocument.Parse(dumped).RootElement.GetProperty("variable");
        await Assert.That(memory.EnumerateArray().Any(v => v.ValueKind == System.Text.Json.JsonValueKind.String
            && v.GetString() == app.Name(user).ToString())).IsTrue();
    }
}
