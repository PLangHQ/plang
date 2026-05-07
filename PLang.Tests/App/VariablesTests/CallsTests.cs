using AppVars = global::App.Variables.@this;

namespace PLang.Tests.App.VariablesTests;

// Variables.Calls — per-call AsyncLocal parameter scope.
// Pushed at concurrency boundaries (e.g. GoalChannel.WriteAsync) so concurrent
// calls don't race on a shared parameter slot. Sequential calls stay leaky.

public class CallsTests
{
    [Test]
    public async Task Push_BindsParametersForGet()
    {
        var vars = new AppVars();
        await using var _ = vars.Calls.Push(new[] { new Data("greeting", "hello") });

        var got = vars.Get("greeting");
        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Push_FrameDisposes_ParameterGoneFromGet()
    {
        var vars = new AppVars();
        var scope = vars.Calls.Push(new[] { new Data("ephemeral", "v1") });
        await Assert.That(vars.Get("ephemeral").Value).IsEqualTo("v1");
        await scope.DisposeAsync();

        var got = vars.Get("ephemeral");
        await Assert.That(got.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Push_FrameWinsOverUnderlyingForSameName()
    {
        var vars = new AppVars();
        vars.Set("x", "underlying");
        await using var _ = vars.Calls.Push(new[] { new Data("x", "framed") });

        await Assert.That(vars.Get("x").Value).IsEqualTo("framed");
    }

    [Test]
    public async Task Push_NestedFrames_InnerWins()
    {
        var vars = new AppVars();
        await using var outer = vars.Calls.Push(new[] { new Data("k", "outer") });
        await using var inner = vars.Calls.Push(new[] { new Data("k", "inner") });

        await Assert.That(vars.Get("k").Value).IsEqualTo("inner");
    }

    [Test]
    public async Task Push_NestedFrames_PoppingInnerRestoresOuter()
    {
        var vars = new AppVars();
        await using var outer = vars.Calls.Push(new[] { new Data("k", "outer") });
        var inner = vars.Calls.Push(new[] { new Data("k", "inner") });
        await Assert.That(vars.Get("k").Value).IsEqualTo("inner");
        await inner.DisposeAsync();

        await Assert.That(vars.Get("k").Value).IsEqualTo("outer");
    }

    [Test]
    public async Task Push_FrameInvisibleToParallelFlows()
    {
        // The whole point: AsyncLocal scoping means concurrent flows each see
        // their own frame, not each other's. Without this, GoalChannel.WriteAsync
        // would race on %!data%.
        var vars = new AppVars();

        var (gotA, gotB) = await TaskA(vars).ContinueWith(async ta =>
        {
            var a = await ta;
            var b = await TaskB(vars);
            return (a, b);
        }).Unwrap();

        // Both saw their own value, neither saw the other's.
        await Assert.That(gotA).IsEqualTo("flow-A");
        await Assert.That(gotB).IsEqualTo("flow-B");

        static async Task<string> TaskA(AppVars v)
        {
            await using var _ = v.Calls.Push(new[] { new Data("who", "flow-A") });
            await Task.Yield();
            return v.Get("who").Value!.ToString()!;
        }
        static async Task<string> TaskB(AppVars v)
        {
            await using var _ = v.Calls.Push(new[] { new Data("who", "flow-B") });
            await Task.Yield();
            return v.Get("who").Value!.ToString()!;
        }
    }

    [Test]
    public async Task Push_ConcurrentFlows_NoRaceOnSharedName()
    {
        // Many concurrent pushes on the same Variables instance, each binding
        // %seen% to its own value, each verifying it reads back its own value.
        // Today's Variables.Set("seen", ...) without a frame would race; this
        // test exists to prove the AsyncLocal frame closes that gap.
        var vars = new AppVars();
        const int n = 200;
        var tasks = new Task<bool>[n];
        for (int i = 0; i < n; i++)
        {
            int mine = i;
            tasks[i] = Task.Run(async () =>
            {
                await using var _ = vars.Calls.Push(new[] { new Data("seen", mine) });
                await Task.Yield();
                var observed = vars.Get("seen").Value;
                return observed is int v && v == mine;
            });
        }

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(b => b)).IsTrue();
    }

    [Test]
    public async Task SetInsideFrame_WritesUnderlying_NotFrame()
    {
        // Goal-body Set still mutates actor-shared Variables. Frame is a read
        // overlay only — matches the LoadUser-leaks-%user% pattern in PLang.
        var vars = new AppVars();
        vars.Set("k", "before");
        var scope = vars.Calls.Push(new[] { new Data("k", "framed") });

        await Assert.That(vars.Get("k").Value).IsEqualTo("framed");
        vars.Set("k", "mutated");
        // Inside the frame the read still resolves through the frame.
        await Assert.That(vars.Get("k").Value).IsEqualTo("framed");

        await scope.DisposeAsync();
        // After dispose, only the underlying mutation remains.
        await Assert.That(vars.Get("k").Value).IsEqualTo("mutated");
    }

    [Test]
    public async Task Push_NullParameters_PushesEmptyFrame()
    {
        var vars = new AppVars();
        vars.Set("x", "underlying");
        await using var _ = vars.Calls.Push(null);

        await Assert.That(vars.Get("x").Value).IsEqualTo("underlying");
    }

    [Test]
    public async Task Contains_ConsultsFrame()
    {
        var vars = new AppVars();
        await using var _ = vars.Calls.Push(new[] { new Data("frameOnly", 42) });
        await Assert.That(vars.Contains("frameOnly")).IsTrue();
    }
}
