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
        await Assert.That(ReferenceEquals(read.Peek().Clr<object>(), user.CallStack)).IsTrue();
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
