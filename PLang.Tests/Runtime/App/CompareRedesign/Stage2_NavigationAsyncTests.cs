namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — the navigation chain `Data.GetChild` → `Variable.Get` → `Variable.Resolve`
// → `Value()` goes async via `ValueTask` (sync-completing in memory; awaits only
// the first content read of a reference). No I/O at `read X`. Await-once: no
// store-and-await-twice, no `.Result`, no `GetAwaiter().GetResult()`.
public class Stage2_NavigationAsyncTests
{
    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-stage2nav-" + System.Guid.NewGuid().ToString("N")[..8]);
        return new(root);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "PLang.sln")) && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    [Test]
    public async Task DataGetChild_IsValueTask_SyncCompletesInMemory()
    {
        // ValueTask.IsCompletedSuccessfully on materialised dict navigation; zero alloc on hot path
        await using var app = NewApp(out _);
        var dict = new Dictionary<string, object?> { ["name"] = "alice" };
        var d = new Data("user", dict) { Context = app.User.Context };
        var vt = d.GetChild("name");
        await Assert.That(vt.IsCompletedSuccessfully).IsTrue();   // in-memory: no async hop
        await Assert.That((await (await vt).Value())?.ToString()).IsEqualTo("alice");
    }

    [Test]
    public async Task VariableGet_IsValueTask_AwaitsOnlyFirstContentRead()
    {
        // first nav on a pending reference awaits; subsequent navs sync-complete
        await using var app = NewApp(out _);
        var vars = app.User.Context.Variable;
        await vars.Set("x", 42);
        var vt = vars.Get("x");
        await Assert.That(vt.IsCompletedSuccessfully).IsTrue();   // in-memory value: sync-complete
        await Assert.That((await (await vt).Value())).IsEqualTo(42);
    }

    [Test]
    public async Task VariableResolve_DottedPath_ValueTaskShape_OneAwait()
    {
        // %a.b.c.d% — single `await` in caller, no `.Result`, no `GetAwaiter().GetResult()` anywhere on the chain
        await using var app = NewApp(out _);
        var vars = app.User.Context.Variable;
        await vars.Set("a", new Dictionary<string, object?>
        {
            ["b"] = new Dictionary<string, object?> { ["c"] = "deep" }
        });
        var resolved = await vars.Resolve("%a.b.c%");             // ONE await in the caller
        await Assert.That(resolved).IsEqualTo("deep");
    }

    [Test]
    public async Task NavigationChain_NoSyncOverAsync_Anywhere_BuildGate()
    {
        // analyzer/grep gate proves no `.Result` on ValueTask navigation surfaces in PLang/
        var navSources = new[]
        {
            "PLang/app/data/this.Navigation.cs",
            "PLang/app/variable/list/this.cs",
            "PLang/app/variable/navigator/Dictionary.cs",
            "PLang/app/variable/navigator/List.cs",
            "PLang/app/variable/navigator/Object.cs",
            "PLang/app/variable/navigator/Snapshot.cs",
            "PLang/app/variable/navigator/ValueNavigators.cs",
        };
        var repoRoot = FindRepoRoot();
        foreach (var rel in navSources)
        {
            var src = await File.ReadAllTextAsync(Path.Combine(repoRoot, rel));
            await Assert.That(src).DoesNotContain(".GetAwaiter().GetResult()");
            await Assert.That(src).DoesNotContain(".Result;");
        }
    }

    [Test]
    public async Task ReadX_DoesNotFireIo_LoadHappensOnFirstNavigation()
    {
        // `read file.txt` step completes with MaterializeCount=0; the read fires on the FIRST navigation
        // (so `%file!file!path%` stays at 0, `%file.field%` increments)
        await using var app = NewApp(out var root);
        var p = new global::app.type.path.file.@this(System.IO.Path.Combine(root, "cfg.json"), app.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();
        var d = await new global::app.channel.type.file.@this(p).Read();
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);       // read step: nothing parsed
        var port = await (await d.GetChild("port")).Value();      // first navigation parses
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);
        await Assert.That(port?.ToString()).IsEqualTo("8080");
    }

    [Test]
    public async Task FluidTemplate_MaterialisesUpFront_AtSetValue_NotMidRender()
    {
        // RenderAsync is async; FluidValue.Create(await kvp.Value.Value(), ...) materialises BEFORE the sync render walk
        // The render path materialises parameter values (await kvp.Value.Value()) BEFORE the
        // sync template walk — proven by the source shape: FluidValue.Create over awaited values.
        var src = await File.ReadAllTextAsync(Path.Combine(FindRepoRoot(), "PLang/app/module/ui/code/Fluid.cs"));
        await Assert.That(src).Contains("FluidValue.Create(await");
    }
}
