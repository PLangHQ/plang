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
var server = new Lazy<global::app.@this>(() => Bound(new global::app.@this(root, wiring: global::app.Wiring.none).Testing()));

// BENCH_MAKER=n: the maker holds n bindings (a debug watch, an on.event): on its variable type's set and remove, a step's and an
// action's start; a sub app is born taking them in
static global::app.@this Bound(global::app.@this maker)
{
    if (!int.TryParse(Environment.GetEnvironmentVariable("BENCH_MAKER"), out var count)) return maker;
    var items = new[] { (maker.variable.type, "set"), (maker.variable.type, "remove"), (maker.type.list["step"], "start"), (maker.type.list["action"], "start") };
    for (var i = 0; i < count; i++)
    {
        var (item, name) = items[i % items.Length];
        maker.@event.Add(item).Add(name)!.after.Add(side => new global::app.@event.binding.@this(side,
            (_, _, context) => Task.FromResult(context.Ok()), maker.actor.list.User, global::app.@event.binding.Scope.actor,
            global::app.@event.binding.@this.Always));
    }
    return maker;
}
global::app.@this Make(bool console) => child
    ? new global::app.@this(root, server.Value.actor.list.User)
    : new global::app.@this(root, wiring: console ? global::app.Wiring.console : global::app.Wiring.none);

// BENCH_NOTEST=1: births without the test session and the signing mock, so the bytes are the birth's alone
global::app.@this Test(global::app.@this app) => Environment.GetEnvironmentVariable("BENCH_NOTEST") == "1" ? app : app.Testing();

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
        for (var i = 0; i < warm; i++) await Test(Make(true)).DisposeAsync();
        var births = new List<double>();
        var alloc0 = GC.GetTotalAllocatedBytes(true);
        var cpu0 = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        for (var i = 0; i < n; i++)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var app = Make(true);
            births.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            await Test(app).DisposeAsync();
        }
        // the alloc counts the birth and its dispose (a request app pays both)
        var kb = (GC.GetTotalAllocatedBytes(true) - alloc0) / (double)n / 1024.0;
        var cpu = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu0).TotalMilliseconds / n;
        births.Sort();
        Console.WriteLine(FormattableString.Invariant(
            $"birth {mode} ms={births[n / 2]:F2} min={births[0]:F2} p90={births[n * 9 / 10]:F2} cpu={cpu:F2} kb={kb:F0}"));
        break;
    }
    case "steps":
    {
        // bytes of each step of a test app's making, averaged over BENCH_N births (this thread's allocations)
        var count = int.TryParse(Environment.GetEnvironmentVariable("BENCH_N"), out var sn) ? sn : 2000;
        var names = new[] { "new app", "test.Open", "new TestSigning", "Register ISigning", "Register IKey", "SetDefault ISigning", "SetDefault IKey", "dispose" };
        var sums = new long[names.Length];
        for (var i = 0; i < 20 + count; i++)
        {
            var at = new long[names.Length + 1];
            at[0] = GC.GetAllocatedBytesForCurrentThread();
            var app = Make(true); at[1] = GC.GetAllocatedBytesForCurrentThread();
            app.test.list.Open(app.actor.list.User); at[2] = GC.GetAllocatedBytesForCurrentThread();
            var provider = new global::PLang.Tests.Shared.TestSigning { IsBuiltIn = true }; at[3] = GC.GetAllocatedBytesForCurrentThread();
            app.Code.Register<global::app.module.signing.code.ISigning>(provider); at[4] = GC.GetAllocatedBytesForCurrentThread();
            app.Code.Register<global::app.module.signing.code.IKey>(provider); at[5] = GC.GetAllocatedBytesForCurrentThread();
            app.Code.SetDefault<global::app.module.signing.code.ISigning>("test-signing"); at[6] = GC.GetAllocatedBytesForCurrentThread();
            app.Code.SetDefault<global::app.module.signing.code.IKey>("test-signing"); at[7] = GC.GetAllocatedBytesForCurrentThread();
            await app.DisposeAsync(); at[8] = GC.GetAllocatedBytesForCurrentThread();
            if (i >= 20) for (var s = 0; s < names.Length; s++) sums[s] += at[s + 1] - at[s];
        }
        for (var s = 0; s < names.Length; s++)
            Console.WriteLine(FormattableString.Invariant($"{sums[s] / (double)count:F0} B  {names[s]}"));
        break;
    }
    case "index":
    {
        // bytes of one action.Index (a search of its step's code) over the actions of the request goal
        await using var app = Make(false).Testing();
        var loaded = await app.goal.Load("/module/condition/if/basic/Condition.test.goal");
        var goal = (global::app.goal.@this)(await loaded.Value())!;
        var actions = goal.Step.Items().SelectMany(s => s.Code.Items()).ToList();
        foreach (var a in actions) _ = a.Index;
        var at0 = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) foreach (var a in actions) _ = a.Index;
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - at0) / 1000.0;
        Console.WriteLine(FormattableString.Invariant($"index actions={actions.Count} bytes-per-pass={bytes:F0}"));
        break;
    }
    case "alloc":
    {
        // allocation ticks (one per ~100 KB, sampled by bytes) by type over BENCH_N births: `type kb-per-birth`
        var count = int.TryParse(Environment.GetEnvironmentVariable("BENCH_N"), out var cn) ? cn : 3000;
        for (var i = 0; i < 20; i++) await Make(true).Testing().DisposeAsync();
        using var ticks = new AllocTicks();
        for (var i = 0; i < count; i++) await Make(true).Testing().DisposeAsync();
        ticks.Report(count);
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
        Console.Error.WriteLine("usage: Bench <birth|request|alloc> <root|child>");
        return 2;
}
return 0;

// The runtime's allocation ticks (GCAllocationTick, one per ~100 KB allocated), summed by type: sampled by bytes, so a
// type's share is its share of what was allocated.
sealed class AllocTicks : System.Diagnostics.Tracing.EventListener
{
    private readonly Dictionary<string, long> _bytes = new();

    protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
    }

    protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick") != true || e.Payload == null) return;
        var names = e.PayloadNames!;
        var type = e.Payload[names.IndexOf("TypeName")] as string ?? "?";
        var amount = Convert.ToInt64(e.Payload[names.IndexOf("AllocationAmount64")]);
        lock (_bytes) _bytes[type] = _bytes.GetValueOrDefault(type) + amount;
    }

    internal void Report(int births)
    {
        Thread.Sleep(2000);
        lock (_bytes)
            foreach (var (type, bytes) in _bytes.OrderByDescending(p => p.Value).Take(60))
                Console.WriteLine(FormattableString.Invariant($"{bytes / 1024.0 / births:F2} {type}"));
    }
}
