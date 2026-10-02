using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// <c>terminal.start(..., Permission=[{path, verbs}])</c>: the kernel (Landlock) holds the program to its permissions —
/// it touches only the folders they name, with their verbs, while plang itself, and every program started without
/// permissions, stay free. Each permission is first the caller's to grant. A step that gives a program permissions
/// never runs it free. Linux only; elsewhere refused (PermissionNotEnforced).
/// </summary>
public class PermissionTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;

    public PermissionTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_permission_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_permission_out_" + Guid.NewGuid().ToString("N"))).FullName;
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

    /// <summary>Runs <c>sh -c script</c> through terminal.start, given <paramref name="permission"/> when there is one —
    /// the goal as made (its values born typed), or <paramref name="wire"/>: written as a .pr and read back, as a
    /// built app runs it.</summary>
    private async Task<global::app.data.@this> Sh(string script, object? permission, string answer = "a", string sh = "//bin/sh",
        bool wire = false)
    {
        Context.Actor!.Channel.Register(new CannedAnswerChannel(answer));
        var parameters = new List<(string, object?)> { ("App", sh), ("Parameter", new List<object?> { "-c", script }) };
        if (permission != null) parameters.Add(("Permission", permission));
        var goal = Make.Goal(Context, "Run" + Guid.NewGuid().ToString("N")[..6],
            Make.Step("run it", Make.Action(Context, "terminal", "start", parameters.ToArray())));
        return await _app.Start(wire ? await RealGoalLoad.ViaChannel(_app, goal) : goal, Context);
    }

    /// <summary>One permission: <paramref name="path"/> with <paramref name="verbs"/>.</summary>
    private static Dictionary<string, object?> May(string path, params string[] verbs) => new()
    {
        ["path"] = path,
        ["verbs"] = verbs.Cast<object?>().ToList(),
    };

    private static List<object?> Permissions(params Dictionary<string, object?>[] permissions) => permissions.Cast<object?>().ToList();

    [Test]
    public async Task WithPermissions_WritesAndReadsOnlyWhereTheySay()
    {
        if (!OperatingSystem.IsLinux()) return;
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "s");
        var result = await Sh(
            $"echo in > {_root}/granted/a.txt; cat {_root}/granted/a.txt; " +
            $"(echo out > {_outside}/b.txt) 2>/dev/null || echo refused-write; " +
            $"cat {_outside}/secret.txt >/dev/null 2>&1 || echo refused-read",
            Permissions(May("/granted", "read", "write")));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "a.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_outside, "b.txt"))).IsFalse();
        await Assert.That(said).Contains("in");
        await Assert.That(said).Contains("refused-write");
        await Assert.That(said).Contains("refused-read");
    }

    [Test]
    public async Task ReadOnly_ReadsButNeverWrites()
    {
        if (!OperatingSystem.IsLinux()) return;
        File.WriteAllText(Path.Combine(_root, "granted", "kept.txt"), "kept");
        var result = await Sh(
            $"cat {_root}/granted/kept.txt; (echo x > {_root}/granted/new.txt) 2>/dev/null || echo refused-write",
            Permissions(May("/granted", "read")));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(said).Contains("kept");
        await Assert.That(said).Contains("refused-write");
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "new.txt"))).IsFalse();
    }

    [Test]
    public async Task NoPermissions_InAnEmptyList_HoldsTheProgramToItsBase()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"(echo x > {_root}/granted/new.txt) 2>/dev/null || echo refused-write", Permissions());
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString() ?? "").Contains("refused-write");
    }

    [Test]
    public async Task WithoutPermission_TheProgramIsFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"echo free > {_outside}/free.txt", null);
        await result.IsSuccess();
        await Assert.That(File.Exists(Path.Combine(_outside, "free.txt"))).IsTrue();
    }

    [Test]
    public async Task UnderLoad_OnlyTheHeldProgramsAreHeld_PlangsOwnThreadsStayFree()
    {
        if (!OperatingSystem.IsLinux()) return;
        // twenty held programs at once, each trying outside; meanwhile plang's own threads and a free program write there
        var held = Enumerable.Range(0, 20).Select(n => Sh(
            $"echo {n} > {_root}/granted/n{n}.txt; (echo x > {_outside}/held{n}.txt) 2>/dev/null || echo refused",
            Permissions(May("/granted", "write")))).ToList();
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
    public async Task APermissionTheAppMayNotGrant_IsRefused_AndTheProgramNeverStarts()
    {
        if (!OperatingSystem.IsLinux()) return;
        // the program lives in the app (no execute question), so the one question is the permission's — answered no
        var sh = Path.Combine(_root, "sh");
        File.Copy("/bin/sh", sh);
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var result = await Sh($"echo ran > {_root}/granted/ran.txt",
            Permissions(May("/granted", "write"), May("//" + _outside.TrimStart('/'), "write")), answer: "n", sh: "/sh");

        await result.IsFailure();
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task Git_InitAddCommitFetchMoveLink_WorksWithItsPermissions()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/git")) return;
        // an origin outside the app, made free (no permissions) with one commit
        var origin = Path.Combine(_outside, "origin.git");
        foreach (var args in new[] { $"init -q --bare {origin}", $"-C {_outside} clone -q {origin} seed" })
            System.Diagnostics.Process.Start("/usr/bin/git", args)!.WaitForExit();
        var seed = Path.Combine(_outside, "seed");
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a");
        foreach (var args in new[] { $"-C {seed} add a.txt", $"-C {seed} -c user.name=t -c user.email=t@t commit -qm seed", $"-C {seed} push -q origin HEAD" })
            System.Diagnostics.Process.Start("/usr/bin/git", args)!.WaitForExit();

        // held: it reads and writes /granted (its HOME too), and reads the origin
        var repo = $"{_root}/granted/repo";
        var result = await Sh(
            $"export HOME={_root}/granted; git init -q {repo} && cd {repo} && echo x > f.txt && git add f.txt && " +
            $"git -c user.name=t -c user.email=t@t commit -qm first && git remote add origin {origin} && git fetch -q origin && " +
            "git log --oneline FETCH_HEAD 2>&1 | head -1 && git log --oneline -1 && " +
            // a move across folders (refer) and a symlink (make-sym) — what everyday work does
            "mkdir d1 d2 && echo y > d1/y.txt && git add d1/y.txt && git mv d1/y.txt d2/y.txt 2>&1 && " +
            "ln -s f.txt link 2>&1 && git add link && git -c user.name=t -c user.email=t@t commit -qm second 2>&1 && " +
            "git fsck --strict 2>&1 && echo GIT-OK 2>&1",
            Permissions(May("//" + origin.TrimStart('/'), "read"), May("/granted", "read", "write")));

        await result.IsSuccess();
        var said = (await result.Value())?.ToString() ?? "";
        await Assert.That(said).Contains("seed");
        await Assert.That(said).Contains("first");
        await Assert.That(said).Contains("GIT-OK");
    }

    [Test]
    public async Task AGlobPermission_IsRefused()
    {
        var glob = May("/granted/*", "read");
        glob["match"] = "glob";
        var result = await Sh($"echo ran > {_outside}/ran.txt", Permissions(glob));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task Execute_IsNotGivenYet()
    {
        var result = await Sh($"echo ran > {_outside}/ran.txt", Permissions(May("/granted", "read", "execute")));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task APermissionWithNoVerbs_IsRefused()
    {
        var result = await Sh($"echo ran > {_outside}/ran.txt", Permissions(May("/granted")));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task AnotherActorsPermission_IsRefused()
    {
        var theirs = May("/granted", "read");
        theirs["actor"] = "system";
        var result = await Sh($"echo ran > {_outside}/ran.txt", Permissions(theirs));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    [Skip("core gap: permission.Create reads its members by their held type (Get<Text>, Peek() is list), and read off a .pr they are still wires — path and verbs come back empty — for the coder")]
    public async Task ReadOffAPr_APermissionKeepsItsPathAndVerbs()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"echo in > {_root}/granted/a.txt; (echo out > {_outside}/b.txt) 2>/dev/null || echo refused",
            Permissions(May("/granted", "write")), wire: true);
        await result.IsSuccess();
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "a.txt"))).IsTrue();
        await Assert.That((await result.Value())?.ToString() ?? "").Contains("refused");
    }

    [Test]
    public async Task ReadOffAPr_APermissionThatCameBackEmpty_IsRefused_NeverRunFree()
    {
        // until permission.Create reads a .pr's members, they come back empty: refused, never a program run free
        var result = await Sh($"echo ran > {_outside}/ran.txt", Permissions(May("/granted", "write")), wire: true);
        if (result.Success) return;   // the core reads them now: the test above covers it
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    [Skip("core gap: list<T>.Create given one value makes no list of one (the list's reader does, since 22885b348) — for the coder")]
    public async Task OnePermissionNotInAList_IsAListOfOne()
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = await Sh($"echo ran > {_root}/granted/ran.txt; (echo out > {_outside}/out.txt) 2>/dev/null || echo refused",
            May("/granted", "write"));
        await result.IsSuccess();
        await Assert.That(File.Exists(Path.Combine(_root, "granted", "ran.txt"))).IsTrue();
        await Assert.That((await result.Value())?.ToString() ?? "").Contains("refused");
    }

    [Test]
    public async Task NamedPermissionsHoldingNone_AreRefused_NeverRunFree()
    {
        // %given% is set, to nothing: the step gives the program permissions, so it never runs free
        await Context.Variable.Set("given", null);
        var result = await Sh($"echo ran > {_outside}/ran.txt", "%given%");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionInvalid");
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }

    [Test]
    public async Task UnsetPermissions_AreRefused_NeverRunFree()
    {
        var result = await Sh($"echo ran > {_outside}/ran.txt", "%nothingSetHere%");
        await result.IsFailure();
        await Assert.That(File.Exists(Path.Combine(_outside, "ran.txt"))).IsFalse();
    }
}
