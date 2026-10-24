// fast-apps' benchmark (tester). It lives in .bot and is never committed into PLang.Tests: run.sh copies it into
// the Types suite for the run and removes it after. Every stage reruns this same file against stage 0's numbers.
namespace PLang.Tests.Types;

public class FastAppsBench
{
    private static string Root => System.Environment.GetEnvironmentVariable("BENCH_ROOT") ?? "/test";
    private static string Output => System.Environment.GetEnvironmentVariable("BENCH_OUT") ?? "/tmp/fast-apps-bench.txt";
    private static string Label => System.Environment.GetEnvironmentVariable("BENCH_LABEL") ?? "";
    private static string Load => System.IO.File.ReadAllText("/proc/loadavg").Split(' ')[0];

    // BENCH_MODE=child (stage 2 on): every app is a child, made by the User of one server-like app made once
    // (`new app.@this(path, actor)`); otherwise a root, as the command line makes it (`new app.@this(path)`).
    // A child is born with console channels (the door has no flag); a root request app is born without.
    private static bool Child => System.Environment.GetEnvironmentVariable("BENCH_MODE") == "child";
    private static readonly System.Lazy<global::app.@this> Server = new(() => new global::app.@this(Root, autoWireConsoleChannels: false).Testing());
    private static global::app.@this Make(bool console) => Child
        ? new global::app.@this(Root, Server.Value.actor.list.User)
        : new global::app.@this(Root, autoWireConsoleChannels: console);
    private static string Mode => Child ? "child" : "root";

    // an app's birth: 20 warm births, then 100 timed; each disposed before the next
    [Test, Explicit]
    public async Task Birth()
    {
        const int warm = 20, n = 100;
        for (var i = 0; i < warm; i++) await Make(true).Testing().DisposeAsync();
        var births = new List<double>();
        var alloc0 = System.GC.GetTotalAllocatedBytes(true);
        var cpu0 = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        for (var i = 0; i < n; i++)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var app = Make(true);
            births.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            await app.Testing().DisposeAsync();
        }
        // the alloc counts the birth and its dispose (a request app pays both)
        var kb = (System.GC.GetTotalAllocatedBytes(true) - alloc0) / (double)n / 1024.0;
        var cpu = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu0).TotalMilliseconds / n;
        births.Sort();
        System.IO.File.AppendAllText(Output,
            $"{Label} {Mode} birth  load {Load}  median {births[n / 2]:F2} ms  min {births[0]:F2}  p90 {births[n * 9 / 10]:F2}  cpu {cpu:F2} ms  alloc {kb:F0} KB (birth + dispose)\n");
    }

    // a request app, 3,000 times: born, a small goal loaded and run, disposed; one at a time. After every 1,000
    // the heap is collected whole and what is still held written down: flat is no leak.
    [Test, Explicit]
    public async Task RequestApps()
    {
        // BENCH_N: how many request apps (default 3,000); the heap is collected and written down every 1,000
        var total = int.TryParse(System.Environment.GetEnvironmentVariable("BENCH_N"), out var n) ? n : 3000;
        const int block = 1000;
        const string goal = "/module/condition/if/basic/Condition.test.goal";
        var failed = 0; string? why = null;
        // the server is made before the first count: what it holds is the process's, not the requests'
        if (Child) _ = Server.Value;
        var held = new List<double> { Held() };
        var alloc0 = System.GC.GetTotalAllocatedBytes(true);
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        double running = 0;
        for (var done = 0; done < total; done += block)
        {
            var b0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < block; i++)
            {
                var app = Make(false).Testing();
                await using (app)
                {
                    var loaded = await app.goal.Load(goal);
                    var result = loaded.Success
                        ? await ((await loaded.Value()) as global::app.goal.@this)!.Start(app.actor.list.User.Context)
                        : loaded;
                    if (!result.Success) { failed++; why ??= result.Error?.Message; }
                }
            }
            running += System.Diagnostics.Stopwatch.GetElapsedTime(b0).TotalSeconds;
            held.Add(Held());
            // BENCH_PAUSE=<dir>: after each block, wait (≤ 10 min) while <dir>/pause-<n> exists, so a heap dump can be taken
            if (System.Environment.GetEnvironmentVariable("BENCH_PAUSE") is { } pause)
            {
                var gate = System.IO.Path.Combine(pause, $"pause-{done + block}");
                System.IO.File.WriteAllText(gate, System.Environment.ProcessId.ToString());
                for (var w = 0; w < 600 && System.IO.File.Exists(gate); w++) await Task.Delay(1000);
            }
        }
        // BENCH_REST=<s>: rest, then collect again — what a library lets go only on its own timer is let go now
        if (int.TryParse(System.Environment.GetEnvironmentVariable("BENCH_REST"), out var rest))
        {
            await Task.Delay(rest * 1000);
            held.Add(Held());
            if (System.Environment.GetEnvironmentVariable("BENCH_PAUSE") is { } after)
            {
                var gate = System.IO.Path.Combine(after, "pause-rest");
                System.IO.File.WriteAllText(gate, System.Environment.ProcessId.ToString());
                for (var w = 0; w < 600 && System.IO.File.Exists(gate); w++) await Task.Delay(1000);
            }
        }
        var seconds = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds;
        var kb = (System.GC.GetTotalAllocatedBytes(true) - alloc0) / (double)total / 1024.0;
        System.IO.File.AppendAllText(Output,
            $"{Label} {Mode} request  load {Load}  {total} apps in {running:F1} s ({running * 1000 / total:F2} ms each, {total / running:F0}/s)  "
            + $"alloc {kb:F0} KB each  held MB {string.Join(" → ", held.Select(h => h.ToString("F1")))}  failed {failed}{(why != null ? $" ({why})" : "")}\n");
        await Assert.That(failed).IsEqualTo(0);
    }

    private static double Held()
    {
        System.GC.Collect(2, System.GCCollectionMode.Forced, blocking: true, compacting: true);
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect(2, System.GCCollectionMode.Forced, blocking: true, compacting: true);
        return System.GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    }
}
