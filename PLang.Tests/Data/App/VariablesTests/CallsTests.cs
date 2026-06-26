using AppVars = global::app.variable.list.@this;

namespace PLang.Tests.App.VariablesTests;

// Variables.Calls — per-flow AsyncLocal scope (mutable overlay).
// Pushed by fork operators (channel fire, parallel foreach iteration, ...).
// Sequential goal.call does *not* push — it shares the caller's flow.
// Inside an active overlay, both reads and writes route through the overlay,
// so a goal that does `set %x% = 2` then `get %x%` sees 2, and siblings stay
// isolated. Disposal drops the overlay and any writes it accumulated.

public class CallsTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/calls-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Push_BindsParametersForGet()
    {
        var vars = new AppVars(_app.User.Context);
        await using var _ = vars.Calls.Push(new[] { _app.Data("greeting", "hello") });

        var got = await vars.Get("greeting");
        await Assert.That(got).IsNotNull();
        await Assert.That((await got!.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Push_FrameDisposes_ParameterGoneFromGet()
    {
        var vars = new AppVars(_app.User.Context);
        var scope = vars.Calls.Push(new[] { _app.Data("ephemeral", "v1") });
        await Assert.That((await (await vars.Get("ephemeral")).Value())?.ToString()).IsEqualTo("v1");
        await scope.DisposeAsync();

        var got = await vars.Get("ephemeral");
        await Assert.That(got.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Push_FrameWinsOverUnderlyingForSameName()
    {
        var vars = new AppVars(_app.User.Context);
        vars.Set("x", "underlying");
        await using var _ = vars.Calls.Push(new[] { _app.Data("x", "framed") });

        await Assert.That((await (await vars.Get("x")).Value())?.ToString()).IsEqualTo("framed");
    }

    [Test]
    public async Task Push_NestedFrames_InnerWins()
    {
        var vars = new AppVars(_app.User.Context);
        await using var outer = vars.Calls.Push(new[] { _app.Data("k", "outer") });
        await using var inner = vars.Calls.Push(new[] { _app.Data("k", "inner") });

        await Assert.That((await (await vars.Get("k")).Value())?.ToString()).IsEqualTo("inner");
    }

    [Test]
    public async Task Push_NestedFrames_PoppingInnerRestoresOuter()
    {
        var vars = new AppVars(_app.User.Context);
        await using var outer = vars.Calls.Push(new[] { _app.Data("k", "outer") });
        var inner = vars.Calls.Push(new[] { _app.Data("k", "inner") });
        await Assert.That((await (await vars.Get("k")).Value())?.ToString()).IsEqualTo("inner");
        await inner.DisposeAsync();

        await Assert.That((await (await vars.Get("k")).Value())?.ToString()).IsEqualTo("outer");
    }

    [Test]
    public async Task Push_ParallelFlows_EachSeesOwnBinding()
    {
        // Two flows that *actually run in parallel* (via Task.WhenAll). AsyncLocal
        // captures the Calls.Current at the await boundary, so each flow's frame
        // is invisible to the other. ContinueWith chaining used to mask this —
        // it's sequential, so the test was tautological.
        var vars = new AppVars(_app.User.Context);

        var (gotA, gotB) = await TaskWhenBoth(TaskA(vars), TaskB(vars));

        await Assert.That(gotA).IsEqualTo("flow-A");
        await Assert.That(gotB).IsEqualTo("flow-B");

        async Task<string> TaskA(AppVars v)
        {
            await using var _ = v.Calls.Push(new[] { _app.Data("who", "flow-A") });
            await Task.Yield();
            return (await (await v.Get("who")).Value())!.ToString()!;
        }
        async Task<string> TaskB(AppVars v)
        {
            await using var _ = v.Calls.Push(new[] { _app.Data("who", "flow-B") });
            await Task.Yield();
            return (await (await v.Get("who")).Value())!.ToString()!;
        }
        static async Task<(string, string)> TaskWhenBoth(Task<string> a, Task<string> b)
        {
            var results = await Task.WhenAll(a, b);
            return (results[0], results[1]);
        }
    }

    [Test]
    public async Task Push_ConcurrentFlows_NoRaceOnSharedName()
    {
        // Many concurrent pushes on the same Variables instance, each binding
        // %seen% to its own value, each verifying it reads back its own value.
        // Without the AsyncLocal overlay this would race on the actor-shared dict.
        var vars = new AppVars(_app.User.Context);
        const int n = 200;
        var tasks = new Task<bool>[n];
        for (int i = 0; i < n; i++)
        {
            int mine = i;
            tasks[i] = Task.Run(async () =>
            {
                await using var _ = vars.Calls.Push(new[] { _app.Data("seen", mine) });
                await Task.Yield();
                var observed = (await vars.Get("seen")).Peek();
                return observed is global::app.type.number.@this v && v.ToInt32() == mine;
            });
        }

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(b => b)).IsTrue();
    }

    [Test]
    public async Task SetInsideOverlay_IsVisibleToSubsequentGet()
    {
        // The PLang-developer expectation: inside a goal that was forked with
        // x=1, `set %x% = 2` then `get %x%` reads 2 — not 1. The overlay is a
        // mutable scope, not a read-only param shadow.
        var vars = new AppVars(_app.User.Context);
        await using var _ = vars.Calls.Push(new[] { _app.Data("x", 1) });

        await Assert.That((await (await vars.Get("x")).Value())?.ToString()).IsEqualTo("1");
        vars.Set("x", 2);
        await Assert.That((await (await vars.Get("x")).Value())?.ToString()).IsEqualTo("2");
    }

    [Test]
    public async Task SetInsideOverlay_DoesNotLeakToUnderlying()
    {
        // Writes inside an overlay stay in the overlay. After dispose, the
        // actor-shared dict is unchanged.
        var vars = new AppVars(_app.User.Context);
        vars.Set("k", "underlying");
        var scope = vars.Calls.Push(null);
        vars.Set("k", "scoped");
        await Assert.That((await (await vars.Get("k")).Value())?.ToString()).IsEqualTo("scoped");

        await scope.DisposeAsync();
        await Assert.That((await (await vars.Get("k")).Value())?.ToString()).IsEqualTo("underlying");
    }

    [Test]
    public async Task SetInsideOverlay_NewName_DoesNotEscape()
    {
        // A name that didn't exist before the push, written inside the overlay,
        // is gone after dispose.
        var vars = new AppVars(_app.User.Context);
        var scope = vars.Calls.Push(null);
        vars.Set("fresh", 42);
        await Assert.That((await (await vars.Get("fresh")).Value())?.ToString()).IsEqualTo("42");

        await scope.DisposeAsync();
        await Assert.That((await vars.Get("fresh")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task SetInsideOverlay_DoesNotLeakToSiblingOverlay()
    {
        // Two parallel flows: one writes into its overlay, the other reads. The
        // reader must NOT see the writer's value. This is the production race
        // GoalChannel.InvokeGoal solves — both fires read %!data% concurrently.
        var vars = new AppVars(_app.User.Context);
        vars.Set("k", "underlying");

        var writerStarted = new TaskCompletionSource<bool>();
        var readerCanRead = new TaskCompletionSource<bool>();

        async Task<string> Writer()
        {
            await using var _ = vars.Calls.Push(null);
            vars.Set("k", "writer-only");
            writerStarted.TrySetResult(true);
            await readerCanRead.Task;          // hold the overlay open
            return (await (await vars.Get("k")).Value())!.ToString()!;
        }
        async Task<string> Reader()
        {
            await writerStarted.Task;          // ensure writer's overlay is live
            await using var _ = vars.Calls.Push(null);
            var seen = (await (await vars.Get("k")).Value())!.ToString()!;
            readerCanRead.TrySetResult(true);
            return seen;
        }

        var results = await Task.WhenAll(Writer(), Reader());
        await Assert.That((results[0])?.ToString()).IsEqualTo("writer-only");
        await Assert.That((results[1])?.ToString()).IsEqualTo("underlying"); // reader didn't see writer's overlay
    }

    [Test]
    public async Task Push_NullParameters_PushesEmptyFrame()
    {
        var vars = new AppVars(_app.User.Context);
        vars.Set("x", "underlying");
        await using var _ = vars.Calls.Push(null);

        await Assert.That((await (await vars.Get("x")).Value())?.ToString()).IsEqualTo("underlying");
    }

    [Test]
    public async Task Contains_ConsultsFrame()
    {
        var vars = new AppVars(_app.User.Context);
        await using var _ = vars.Calls.Push(new[] { _app.Data("frameOnly", 42) });
        await Assert.That(vars.Contains("frameOnly")).IsTrue();
    }
}
