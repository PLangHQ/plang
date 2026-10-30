using System.Runtime;
using Goal = Bench.goal.@this;

namespace Bench.mode.cold;

/// <summary>One fresh process: what the first of everything costs, then the same again, with the JIT's share of each
/// (run it once per process).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var sinceStart = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        Say($"process start to Main: {sinceStart:F0} ms (jit so far {JitInfo.GetCompilationTime().TotalMilliseconds:F0} ms, {JitInfo.GetCompiledMethodCount()} methods)");
        if (Setting.Child) await Step("server app (cold, as root)", () => Task.FromResult(Born.Server));
        for (var pass = 1; pass <= 2; pass++)
        {
            var app = await Step($"#{pass} app born", () => Task.FromResult(Born.Request()));
            var goal = new Goal(app, Setting.Goal);
            var loaded = await Step($"#{pass} goal loaded", goal.Load);
            await Step($"#{pass} goal run (first)", () => goal.Start(loaded));
            await Step($"#{pass} goal run (again)", () => goal.Start(loaded));
            await app.DisposeAsync();
        }
        return 0;
    }

    // What one step took: its time, and the JIT's share of it (time and methods compiled).
    private async Task<T> Step<T>(string name, Func<Task<T>> operation)
    {
        var jit = JitInfo.GetCompilationTime();
        var methods = JitInfo.GetCompiledMethodCount();
        var step = new meter.@this();
        var result = await operation();
        Say($"{name,-34} {step.Elapsed.TotalMilliseconds,8:F1} ms   jit {(JitInfo.GetCompilationTime() - jit).TotalMilliseconds,7:F1} ms  {JitInfo.GetCompiledMethodCount() - methods,5} methods");
        return result;
    }
}
