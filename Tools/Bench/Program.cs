// The fast-apps benchmark: what an app costs to make and run. Tools/Bench/run.sh runs it with everything else it
// measures; this is the part that needs the app's internals.
//
//   Bench birth   <root|child>     an app's birth: 20 warm, then 100 timed, each disposed before the next
//   Bench request <root|child>     a request app BENCH_N (3,000) times: born, a goal loaded and run, disposed
//
// Env: BENCH_ROOT (the folder of the app; default <repo>/test), BENCH_N, BENCH_REST (seconds to rest before the
// last heap count; 0 = none). One line on stdout per run, `key=value` pairs, which run.sh reads.
//
// root: an app as the command line makes it (`new app.@this(path)`). child: made through the door by the User of
// one server-like app made once (`new app.@this(path, actor)`) — a request app in a server.
using PLang.Tests;

var mode = args.Length > 1 ? args[1] : "root";
var child = mode == "child";
var root = Environment.GetEnvironmentVariable("BENCH_ROOT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../test"));
var server = new Lazy<global::app.@this>(() => new global::app.@this(root, wiring: global::app.Wiring.none).Testing());
global::app.@this Make(bool console) => child
    ? new global::app.@this(root, server.Value.actor.list.User)
    : new global::app.@this(root, wiring: console ? global::app.Wiring.console : global::app.Wiring.none);

static double Held()
{
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    return GC.GetTotalMemory(false) / 1024.0 / 1024.0;
}

switch (args.Length > 0 ? args[0] : "")
{
    case "birth":
    {
        const int warm = 20, n = 100;
        for (var i = 0; i < warm; i++) await Make(true).Testing().DisposeAsync();
        var births = new List<double>();
        var alloc0 = GC.GetTotalAllocatedBytes(true);
        var cpu0 = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        for (var i = 0; i < n; i++)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var app = Make(true);
            births.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            await app.Testing().DisposeAsync();
        }
        // the alloc counts the birth and its dispose (a request app pays both)
        var kb = (GC.GetTotalAllocatedBytes(true) - alloc0) / (double)n / 1024.0;
        var cpu = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu0).TotalMilliseconds / n;
        births.Sort();
        Console.WriteLine(FormattableString.Invariant(
            $"birth {mode} ms={births[n / 2]:F2} min={births[0]:F2} p90={births[n * 9 / 10]:F2} cpu={cpu:F2} kb={kb:F0}"));
        break;
    }
    case "request":
    {
        var total = int.TryParse(Environment.GetEnvironmentVariable("BENCH_N"), out var n) ? n : 3000;
        var rest = int.TryParse(Environment.GetEnvironmentVariable("BENCH_REST"), out var r) ? r : 0;
        const int block = 1000;
        const string goal = "/module/condition/if/basic/Condition.test.goal";
        var failed = 0; string? why = null;
        // the server is made before the first count: what it holds is the process's, not the requests'
        if (child) _ = server.Value;
        var held = new List<double> { Held() };
        var alloc0 = GC.GetTotalAllocatedBytes(true);
        double running = 0;
        for (var done = 0; done < total; done += block)
        {
            var b0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < Math.Min(block, total - done); i++)
            {
                await using var app = Make(false).Testing();
                var loaded = await app.goal.Load(goal);
                var result = loaded.Success
                    ? await ((await loaded.Value()) as global::app.goal.@this)!.Start(app.actor.list.User.Context)
                    : loaded;
                if (!result.Success) { failed++; why ??= result.Error?.Message; }
            }
            running += System.Diagnostics.Stopwatch.GetElapsedTime(b0).TotalSeconds;
            held.Add(Held());
        }
        // a rest, then collect again: what a library lets go only on its own timer is let go now
        if (rest > 0) { await Task.Delay(rest * 1000); held.Add(Held()); }
        var kb = (GC.GetTotalAllocatedBytes(true) - alloc0) / (double)total / 1024.0;
        Console.WriteLine(FormattableString.Invariant(
            $"request {mode} ms={running * 1000 / total:F2} kb={kb:F0} held={string.Join(">", held.Select(h => h.ToString("F1")))} failed={failed}{(why != null ? $" why={why.Replace(' ', '_')}" : "")}"));
        return failed == 0 ? 0 : 1;
    }
    default:
        Console.Error.WriteLine("usage: Bench <birth|request> <root|child>");
        return 2;
}
return 0;
