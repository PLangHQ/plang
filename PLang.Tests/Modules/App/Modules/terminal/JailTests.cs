using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// <c>terminal.start(..., Jail={read, write})</c>: the kernel (Landlock) holds the program to its folders — it writes
/// and reads only where the jail says, while plang itself, and every program started without a jail, stay free.
/// Linux only; elsewhere a jail is refused (JailUnavailable), never ignored.
/// </summary>
public class JailTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;

    public JailTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_jail_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_jail_out_" + Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(_root, "granted"));
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    /// <summary>Runs <c>sh -c script</c> through terminal.start, held to <paramref name="jail"/> when given.</summary>
    private async Task<global::app.data.@this> Sh(string script, Dictionary<string, object?>? jail, string answer = "a", string sh = "//bin/sh")
    {
        Context.Actor!.Channel.Register(new CannedAnswerChannel(answer));
        var parameters = new List<(string, object?)> { ("App", sh), ("Parameter", new List<object?> { "-c", script }) };
        if (jail != null) parameters.Add(("Jail", jail));
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(Context, "Run" + Guid.NewGuid().ToString("N")[..6],
            Make.Step("run it", Make.Action(Context, "terminal", "start", parameters.ToArray()))));
        return await _app.Start(goal, Context);
    }

    private static Dictionary<string, object?> Jail(string[]? read = null, string[]? write = null) => new()
    {
        ["read"] = (read ?? []).Cast<object?>().ToList(),
        ["write"] = (write ?? []).Cast<object?>().ToList(),
    };

    [Test]
    public async Task Jailed_WritesAndReadsOnlyWhereItsJailSays()
    {
        if (!OperatingSystem.IsLinux()) return;
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "s");
        var result = await Sh(
            $"echo in > {_root}/granted/a.txt; " +
            $"(echo out > {_outside}/b.txt) 2>/dev/null || echo refused-write; " +
            $"cat {_outside}/secret.txt >/dev/null 2>&1 || echo refused-read",
            Jail(write: ["/granted"]));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "a.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_outside, "b.txt"))).IsFalse();
        await Assert.That(said).Contains("refused-write");
        await Assert.That(said).Contains("refused-read");
    }

    [Test]
    public async Task WithoutAJail_TheProgramIsFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"echo free > {_outside}/free.txt", null);
        await result.IsSuccess();
        await Assert.That(File.Exists(Path.Combine(_outside, "free.txt"))).IsTrue();
    }

    [Test]
    public async Task UnderLoad_OnlyTheJailedProgramsAreHeld_PlangsOwnThreadsStayFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        // twenty jailed programs at once, each trying outside; meanwhile plang's own threads and a free program write there
        var jailed = Enumerable.Range(0, 20).Select(n => Sh(
            $"echo {n} > {_root}/granted/n{n}.txt; (echo x > {_outside}/jailed{n}.txt) 2>/dev/null || echo refused",
            Jail(write: ["/granted"]))).ToList();
        var pool = Enumerable.Range(0, 200).Select(n => Task.Run(() => File.WriteAllText(Path.Combine(_outside, $"pool{n}.txt"), "x"))).ToList();
        var free = Sh($"echo free > {_outside}/free.txt", null);
        await Task.WhenAll(jailed);
        await Task.WhenAll(pool);
        await (await free).IsSuccess();

        foreach (var run in jailed) await Assert.That((await (await run).Value())?.ToString() ?? "").Contains("refused");
        await Assert.That(Enumerable.Range(0, 20).All(n => File.Exists(Path.Combine(_root, "granted", $"n{n}.txt")))).IsTrue();
        await Assert.That(Enumerable.Range(0, 20).Any(n => File.Exists(Path.Combine(_outside, $"jailed{n}.txt")))).IsFalse();
        await Assert.That(Enumerable.Range(0, 200).All(n => File.Exists(Path.Combine(_outside, $"pool{n}.txt")))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_outside, "free.txt"))).IsTrue();
        // plang's own (test) thread still writes outside afterwards
        File.WriteAllText(Path.Combine(_outside, "after.txt"), "x");
    }

    [Test]
    public async Task AFolderTheAppMayNotWrite_IsRefused_AndTheProgramNeverStarts()
    {
        if (!OperatingSystem.IsLinux()) return;
        // the program lives in the app (no execute question), so the one question is the jail's folder — answered no
        var sh = Path.Combine(_root, "sh");
        File.Copy("/bin/sh", sh);
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var result = await Sh($"echo ran > {_root}/granted/ran.txt", Jail(write: ["/granted", "//" + _outside.TrimStart('/')]), answer: "n", sh: "/sh");

        await result.IsFailure();
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task AJailWithAMemberItDoesNotHave_Declines()
    {
        var result = await Sh("true", new Dictionary<string, object?> { ["exec"] = new List<object?> { "/granted" } });
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("read and write");
    }
}
