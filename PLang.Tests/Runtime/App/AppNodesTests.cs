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

        var read = await Read("%!app.callstack%", user);

        await Assert.That(read.IsInitialized).IsTrue();
        await Assert.That(ReferenceEquals(read.Peek(), user.CallStack)).IsTrue();
    }

    // Every way a program reads the call stack still reads through, the stack being a plang value.
    [Test]
    public async Task Callstack_ProgramReads_StillReadThrough()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var goal = new global::app.goal.@this { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", user) };
        await using var frame = user.CallStack.Push(goal);
        frame.Tag(new global::app.type.item.dict.@this(new System.Collections.Generic.Dictionary<string, object?> { ["owner"] = "ingi" }), user);
        frame.Record(new global::app.error.Error("seen", "Seen", 400), user);

        await Assert.That((await (await Read("%!callStack.Current.Depth%", user)).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await Read("%!callStack.Scope%", user)).Peek()).IsSameReferenceAs(frame);
        await Assert.That((await (await Read("%!callStack.Scope.Tags.owner%", user)).Value())?.ToString()).IsEqualTo("ingi");
        await Assert.That((await (await Read("%!callStack.Audit.Count%", user)).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await Read("%!callStack.Audit%", user)).IsInitialized).IsTrue();
        await Assert.That((await Read("%!callStack.Current.Diffs%", user)).Success).IsTrue();
        await Assert.That((await Read("%!callStack%", user)).Peek()).IsSameReferenceAs(user.CallStack);
    }

    // The call stack is a plang value: written, it shows its frame in play and the run's errors — never the
    // variables or parameters an error keeps; dumped, it completes.
    [Test]
    public async Task Callstack_IsAPlangValue_WrittenWithoutVariables_AndDumped()
    {
        await using var app = NewApp();
        var user = app.actor.list.User.Context;
        var goal = new global::app.goal.@this { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", user) };
        await user.Variable.Set("secret", user.Ok("s3cr3t-value"));
        await using var frame = user.CallStack.Push(goal);
        var error = new global::app.error.Error("it failed here", "Boom", 400) { Variables = user.Variable.Snapshot() };
        frame.Record(error, user);

        using var ms = new System.IO.MemoryStream();
        var written = await user.Format("application/json").Encode(ms, user.Ok(user.CallStack), user);
        await written.IsSuccess();
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).Contains(frame.Id);
        await Assert.That(json).Contains("it failed here");
        await Assert.That(json).DoesNotContain("s3cr3t-value");
        await Assert.That(json.ToLowerInvariant()).DoesNotContain("\"variables\"");
        await Assert.That(json.ToLowerInvariant()).DoesNotContain("\"params\"");

        var dumped = await user.CallStack.Debug(user);
        await Assert.That(dumped).Contains(frame.Id);
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
}
