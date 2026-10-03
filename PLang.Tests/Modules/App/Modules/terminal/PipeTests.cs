using PLang.Tests.App.Types.PathTests.Contract;
using Spawned = app.module.terminal.code.child.spawned.@this;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// <c>terminal.open(…, Pipe=true)</c>: the program gets a pipe pair beside its standard streams — it reads fd 3 and
/// writes fd 4, as Chromium's DevTools pipe does — reached as <c>%program.pipe%</c>, a stream channel whose messages
/// each end with its End (a newline unless set; DevTools' is NUL). plang spawns such a program itself (posix_spawn,
/// Linux): its stdio are pipes, its exit is its code, it leads a process group that ends whole, and a program held to
/// permissions is spawned held.
/// </summary>
public class PipeTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public PipeTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_pipe_" + Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(_root, "granted"));
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private static System.Diagnostics.ProcessStartInfo Sh(string script)
    {
        var info = new System.Diagnostics.ProcessStartInfo("/bin/sh");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return info;
    }

    [Test]
    public async Task AFramedChannel_ReadsOneMessageAtATime_EndedByItsEnd()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("first\0second\0unended");
        var channel = new global::app.channel.type.stream.@this("pipe", new MemoryStream(bytes), ownsStream: true)
            { Framed = true, End = "\0", Actor = Context.Actor! };
        await Assert.That((await (await channel.Read()).Value())?.ToString()).IsEqualTo("first");
        await Assert.That((await (await channel.Read()).Value())?.ToString()).IsEqualTo("second");
        var ended = await channel.Read();
        await Assert.That(ended.Success).IsFalse().Because("a part never ended is no message");
        await Assert.That(ended.Error!.Key).IsEqualTo("ChannelEnded");
    }

    [Test]
    public async Task ASpawnedProgram_HasItsStdio_AndItsExitCode()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var child = Spawned.Start(Sh("read line; echo \"got $line\"; echo oops >&2; exit 3"), pipe: false);
        await child.Input.WriteLineAsync("hi");
        child.Input.Close();
        await Assert.That((await child.Output.ReadToEndAsync()).Trim()).IsEqualTo("got hi");
        await Assert.That((await child.Error.ReadToEndAsync()).Trim()).IsEqualTo("oops");
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(child.ExitCode).IsEqualTo(3);
        await Assert.That(child.Pipe).IsNull();
    }

    [Test]
    public async Task ThePipePair_IsFd3In_AndFd4Out()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var child = Spawned.Start(Sh("cat <&3 >&4"), pipe: true);
        var pipe = child.Pipe!;
        await pipe.WriteAsync("ping\0"u8.ToArray());
        await pipe.FlushAsync();
        var back = new byte[5];
        await pipe.ReadExactlyAsync(back).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(System.Text.Encoding.UTF8.GetString(back)).IsEqualTo("ping\0");
        child.Kill();
    }

    [Test]
    public async Task Kill_EndsTheWholeGroup()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var child = Spawned.Start(Sh("sleep 60 & echo $!; wait"), pipe: true);
        var grandchild = int.Parse((await child.Output.ReadLineAsync())!.Trim());
        child.Kill();
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(child.ExitCode).IsEqualTo(128 + 9);
        var gone = false;
        for (var i = 0; i < 50 && !gone; i++)
        {
            var stat = $"/proc/{grandchild}/stat";
            gone = !File.Exists(stat) || File.ReadAllText(stat).Split(' ')[2] == "Z";
            if (!gone) await Task.Delay(100);
        }
        await Assert.That(gone).IsTrue().Because("the program's own children end with it: it leads their group");
    }

    // What ends Chromium when plang dies, however it dies (kill -9 too): the kernel closes plang's ends of the pipe, the
    // program reads EOF on fd 3 and quits. Only if no one else holds plang's ends — not a program .NET starts after it,
    // nor one plang spawns after it (each end is close-on-exec).
    [Test]
    public async Task PlangsEndsClosing_EndTheProgram_WhileItsOtherProgramsRun()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var child = Spawned.Start(Sh("cat <&3 >/dev/null; exit 7"), pipe: true);
        using var managed = new global::app.module.terminal.code.child.managed.@this(System.Diagnostics.Process.Start(Sh("sleep 30"))!);
        using var spawned = Spawned.Start(Sh("sleep 30"), pipe: false);
        try
        {
            await child.Pipe!.DisposeAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(child.ExitCode).IsEqualTo(7).Because("EOF on fd 3 ended it: no other program holds plang's ends");
        }
        finally
        {
            managed.Kill();
            spawned.Kill();
        }
    }

    /// <summary>terminal.open with Pipe: the program, its pipe a channel of its own.</summary>
    private async Task<global::app.module.terminal.type.process.@this> Open(string script, params (string, object?)[] more)
    {
        Context.Actor!.Channel.Register(new CannedAnswerChannel("a"));
        var parameters = new List<(string, object?)> { ("App", "//bin/sh"), ("Parameter", new List<object?> { "-c", script }), ("Pipe", true) };
        parameters.AddRange(more);
        var goal = Make.Goal(Context, "Open" + Guid.NewGuid().ToString("N")[..6],
            Make.Step("open it", Make.Action(Context, "terminal", "open", parameters.ToArray())));
        var opened = await _app.Start(goal, Context);
        await opened.IsSuccess();
        return (await opened.Value() as global::app.module.terminal.type.process.@this)!;
    }

    [Test]
    public async Task OpenWithPipe_GivesTheProgramsPipe_AsAChannel()
    {
        if (!OperatingSystem.IsLinux()) return;
        var program = await Open("cat <&3 >&4");
        var pipe = program.pipe!;
        pipe.End = "\0";
        await (await pipe.Write(Context.Ok((global::app.type.item.text.@this)"{\"id\":1}"))).IsSuccess();
        var back = await pipe.Read().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That((await back.Value())?.ToString()).IsEqualTo("{\"id\":1}");
        program.Kill();
    }

    [Test]
    public async Task OpenWithPipe_HeldToPermissions_IsSpawnedHeld()
    {
        if (!OperatingSystem.IsLinux()) return;
        var outside = Path.Combine(Path.GetTempPath(), "plang_pipe_out_" + Guid.NewGuid().ToString("N"));
        var program = await Open($"(echo x > {outside}) 2>/dev/null && echo free >&4 || echo held >&4; printf '\\0' >&4; cat <&3 >&4",
            ("Permission", new List<object?> { new Dictionary<string, object?> { ["path"] = "/granted", ["verbs"] = new List<object?> { "read" } } }));
        var pipe = program.pipe!;
        pipe.End = "\0";
        var said = await pipe.Read().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That((await said.Value())?.ToString()?.Trim()).IsEqualTo("held");
        await Assert.That(File.Exists(outside)).IsFalse();
        program.Kill();
    }
}
