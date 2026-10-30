using Goal = Bench.goal.@this;

namespace Bench.mode.repeat;

/// <summary>One app, one goal load, the goal run BENCH_N times: the bytes of the first run, the second, the third and the
/// later ones' average, and how much of the time the garbage collector took.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var count = Setting.Count(50);
        await using var app = Born.Request();
        var goal = new Goal(app, Setting.Goal);
        var loaded = await goal.Load();
        var each = new List<long>();
        // with BENCH_TICKS=1 the types allocated by the runs after the first three, as one run's KB
        for (var warm = 0; warm < 3 && Setting.Ticks; warm++) await goal.Start(loaded);
        using var sampled = Setting.Ticks ? new ticks.@this() : null;
        var pause = GC.GetTotalPauseDuration();
        int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
        var run = new meter.@this();
        for (var i = 0; i < count; i++)
        {
            var lap = new meter.@this();
            await goal.Start(loaded);
            each.Add(lap.Lap());
        }
        var wall = run.Elapsed;
        var paused = GC.GetTotalPauseDuration() - pause;
        Say($"gc: wall={wall.TotalMilliseconds:F0} ms pause={paused.TotalMilliseconds:F1} ms ({100 * paused / wall:F1}%) gen0={GC.CollectionCount(0) - gen0} gen1={GC.CollectionCount(1) - gen1} gen2={GC.CollectionCount(2) - gen2} per run={wall.TotalMilliseconds * 1000 / count:F0} us");
        Say($"repeat {Setting.Kind} run1={each[0] / 1024.0:F0} run2={each[1] / 1024.0:F0} run3={each[2] / 1024.0:F0} later_avg={each.Skip(3).Average() / 1024.0:F0} KB (n={count})");
        sampled?.Report(count);
        return 0;
    }
}
