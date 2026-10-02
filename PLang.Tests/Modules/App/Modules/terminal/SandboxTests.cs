using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// <c>terminal.start(..., Sandbox={read, write})</c>: the kernel (Landlock) holds the program to its folders — it writes
/// and reads only where the sandbox says, while plang itself, and every program started without one, stay free. A step
/// that names a sandbox never runs its program free. Linux only; elsewhere a sandbox is refused (SandboxUnavailable).
/// </summary>
public class SandboxTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;

    public SandboxTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_sandbox_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_sandbox_out_" + Guid.NewGuid().ToString("N"))).FullName;
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

    /// <summary>Runs <c>sh -c script</c> through terminal.start, in <paramref name="sandbox"/> when given.</summary>
    private async Task<global::app.data.@this> Sh(string script, object? sandbox, string answer = "a", string sh = "//bin/sh")
    {
        Context.Actor!.Channel.Register(new CannedAnswerChannel(answer));
        var parameters = new List<(string, object?)> { ("App", sh), ("Parameter", new List<object?> { "-c", script }) };
        if (sandbox != null) parameters.Add(("Sandbox", sandbox));
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(Context, "Run" + Guid.NewGuid().ToString("N")[..6],
            Make.Step("run it", Make.Action(Context, "terminal", "start", parameters.ToArray()))));
        return await _app.Start(goal, Context);
    }

    private static Dictionary<string, object?> Sandbox(string[]? read = null, string[]? write = null) => new()
    {
        ["read"] = (read ?? []).Cast<object?>().ToList(),
        ["write"] = (write ?? []).Cast<object?>().ToList(),
    };

    [Test]
    public async Task InASandbox_WritesAndReadsOnlyWhereItSays()
    {
        if (!OperatingSystem.IsLinux()) return;
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "s");
        var result = await Sh(
            $"echo in > {_root}/granted/a.txt; " +
            $"(echo out > {_outside}/b.txt) 2>/dev/null || echo refused-write; " +
            $"cat {_outside}/secret.txt >/dev/null 2>&1 || echo refused-read",
            Sandbox(write: ["/granted"]));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "a.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_outside, "b.txt"))).IsFalse();
        await Assert.That(said).Contains("refused-write");
        await Assert.That(said).Contains("refused-read");
    }

    [Test]
    public async Task WithoutASandbox_TheProgramIsFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"echo free > {_outside}/free.txt", null);
        await result.IsSuccess();
        await Assert.That(File.Exists(Path.Combine(_outside, "free.txt"))).IsTrue();
    }

    [Test]
    public async Task UnderLoad_OnlyTheSandboxedProgramsAreHeld_PlangsOwnThreadsStayFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        // twenty sandboxed programs at once, each trying outside; meanwhile plang's own threads and a free program write there
        var held = Enumerable.Range(0, 20).Select(n => Sh(
            $"echo {n} > {_root}/granted/n{n}.txt; (echo x > {_outside}/held{n}.txt) 2>/dev/null || echo refused",
            Sandbox(write: ["/granted"]))).ToList();
        var pool = Enumerable.Range(0, 200).Select(n => Task.Run(() => File.WriteAllText(Path.Combine(_outside, $"pool{n}.txt"), "x"))).ToList();
        var free = Sh($"echo free > {_outside}/free.txt", null);
        await Task.WhenAll(held);
        await Task.WhenAll(pool);
        await (await free).IsSuccess();

        foreach (var run in held) await Assert.That((await (await run).Value())?.ToString() ?? "").Contains("refused");
        await Assert.That(Enumerable.Range(0, 20).All(n => File.Exists(Path.Combine(_root, "granted", $"n{n}.txt")))).IsTrue();
        await Assert.That(Enumerable.Range(0, 20).Any(n => File.Exists(Path.Combine(_outside, $"held{n}.txt")))).IsFalse();
        await Assert.That(Enumerable.Range(0, 200).All(n => File.Exists(Path.Combine(_outside, $"pool{n}.txt")))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_outside, "free.txt"))).IsTrue();
        // plang's own (test) thread still writes outside afterwards
        File.WriteAllText(Path.Combine(_outside, "after.txt"), "x");
    }

    [Test]
    public async Task AFolderTheAppMayNotWrite_IsRefused_AndTheProgramNeverStarts()
    {
        if (!OperatingSystem.IsLinux()) return;
        // the program lives in the app (no execute question), so the one question is the sandbox's folder — answered no
        var sh = Path.Combine(_root, "sh");
        File.Copy("/bin/sh", sh);
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var result = await Sh($"echo ran > {_root}/granted/ran.txt", Sandbox(write: ["/granted", "//" + _outside.TrimStart('/')]), answer: "n", sh: "/sh");

        await result.IsFailure();
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task Git_InitAddCommitFetchMoveLink_WorksInsideItsSandbox()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/git")) return;
        // an origin outside the app, made free (no sandbox) with one commit
        var origin = Path.Combine(_outside, "origin.git");
        foreach (var args in new[] { $"init -q --bare {origin}", $"-C {_outside} clone -q {origin} seed" })
            System.Diagnostics.Process.Start("/usr/bin/git", args)!.WaitForExit();
        var seed = Path.Combine(_outside, "seed");
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a");
        foreach (var args in new[] { $"-C {seed} add a.txt", $"-C {seed} -c user.name=t -c user.email=t@t commit -qm seed", $"-C {seed} push -q origin HEAD" })
            System.Diagnostics.Process.Start("/usr/bin/git", args)!.WaitForExit();

        // in the sandbox: it writes only /granted (its HOME too), and reads the origin
        var repo = $"{_root}/granted/repo";
        var result = await Sh(
            $"export HOME={_root}/granted; git init -q {repo} && cd {repo} && echo x > f.txt && git add f.txt && " +
            $"git -c user.name=t -c user.email=t@t commit -qm first && git remote add origin {origin} && git fetch -q origin && " +
            "git log --oneline FETCH_HEAD 2>&1 | head -1 && git log --oneline -1 && " +
            // a move across folders (needs refer) and a symlink (needs make-sym) — what everyday work does
            "mkdir d1 d2 && echo y > d1/y.txt && git add d1/y.txt && git mv d1/y.txt d2/y.txt 2>&1 && " +
            "ln -s f.txt link 2>&1 && git add link && git -c user.name=t -c user.email=t@t commit -qm second 2>&1 && " +
            "git fsck --strict 2>&1 && echo GIT-OK 2>&1",
            Sandbox(read: ["//" + origin.TrimStart('/')], write: ["/granted"]));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(said).Contains("seed");
        await Assert.That(said).Contains("first");
        await Assert.That(said).Contains("GIT-OK");
    }

    [Test]
    public async Task ASandboxWithAMemberItDoesNotHave_Declines()
    {
        var result = await Sh($"echo ran > {_root}/granted/ran.txt", new Dictionary<string, object?> { ["exec"] = new List<object?> { "/granted" } });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SandboxInvalid");
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task OneFolderNotInAList_IsRefused()
    {
        var result = await Sh($"echo ran > {_root}/granted/ran.txt", new Dictionary<string, object?> { ["write"] = "/granted" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SandboxInvalid");
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task ANamedSandboxHoldingNone_IsRefused_NeverRunFree()
    {
        // %box% is set, to nothing: the step names a sandbox, so the program never runs free
        await Context.Variable.Set("box", null);
        var result = await Sh($"echo ran > {_outside}/ran.txt", "%box%");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SandboxInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task ASandbox_AnswersItsFolders_ByReadAndWrite()
    {
        var written = new global::app.data.@this("box", new Dictionary<string, object?> { ["write"] = new List<object?> { "/granted" } }, context: Context);
        var box = await global::app.data.@this<global::app.module.terminal.type.sandbox.@this>.From(written).Value();
        await Assert.That(box).IsNotNull();
        var write = await box!.Get(written, "write");
        var read = await box.Get(written, "read");
        await Assert.That((await write.Value() as global::app.type.item.list.@this)?.Rows(Context).Count()).IsEqualTo(1);
        await Assert.That((await read.Value() as global::app.type.item.list.@this)?.Rows(Context).Count()).IsEqualTo(0);
    }

    [Test]
    public async Task AnUnsetSandbox_IsRefused_NeverRunFree()
    {
        var result = await Sh($"echo ran > {_outside}/ran.txt", "%nothingSetHere%");
        await result.IsFailure();
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }
}
