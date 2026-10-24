// fast-apps' benchmark (tester). It lives in .bot and is never committed into PLang.Tests: run.sh copies it into
// the Types suite for the run and removes it after. Every stage reruns this same file against stage 0's numbers.
namespace PLang.Tests.Types;

public class FastAppsBench
{
    private static string Root => System.Environment.GetEnvironmentVariable("BENCH_ROOT") ?? "/test";
    private static string Output => System.Environment.GetEnvironmentVariable("BENCH_OUT") ?? "/tmp/fast-apps-bench.txt";
    private static string Label => System.Environment.GetEnvironmentVariable("BENCH_LABEL") ?? "";
    private static string Load => System.IO.File.ReadAllText("/proc/loadavg").Split(' ')[0];

    // an app's birth: 20 warm births, then 100 timed; each disposed before the next
    [Test, Explicit]
    public async Task Birth()
    {
        const int warm = 20, n = 100;
        for (var i = 0; i < warm; i++) await new global::app.@this(Root).Testing().DisposeAsync();
        var births = new List<double>();
        var alloc0 = System.GC.GetTotalAllocatedBytes(true);
        var cpu0 = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        for (var i = 0; i < n; i++)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var app = new global::app.@this(Root);
            births.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            await app.Testing().DisposeAsync();
        }
        // the alloc counts the birth and its dispose (a request app pays both)
        var kb = (System.GC.GetTotalAllocatedBytes(true) - alloc0) / (double)n / 1024.0;
        var cpu = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu0).TotalMilliseconds / n;
        births.Sort();
        System.IO.File.AppendAllText(Output,
            $"{Label} birth  load {Load}  median {births[n / 2]:F2} ms  min {births[0]:F2}  p90 {births[n * 9 / 10]:F2}  cpu {cpu:F2} ms  alloc {kb:F0} KB (birth + dispose)\n");
    }

    // a request app, 3,000 times: born, a small goal loaded and run, disposed; one at a time. After every 1,000
    // the heap is collected whole and what is still held written down: flat is no leak.
    [Test, Explicit]
    public async Task RequestApps()
    {
        const int total = 3000, block = 1000;
        const string goal = "/module/condition/if/basic/Condition.test.goal";
        var failed = 0; string? why = null;
        var held = new List<double> { Held() };
        var alloc0 = System.GC.GetTotalAllocatedBytes(true);
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        double running = 0;
        for (var done = 0; done < total; done += block)
        {
            var b0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < block; i++)
            {
                var app = new global::app.@this(Root, autoWireConsoleChannels: false).Testing();
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
        }
        var seconds = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds;
        var kb = (System.GC.GetTotalAllocatedBytes(true) - alloc0) / (double)total / 1024.0;
        System.IO.File.AppendAllText(Output,
            $"{Label} request  load {Load}  {total} apps in {running:F1} s ({running * 1000 / total:F2} ms each, {total / running:F0}/s)  "
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
