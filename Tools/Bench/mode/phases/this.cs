using Goal = Bench.goal.@this;

namespace Bench.mode.phases;

/// <summary>A request app's bytes by phase (this thread), averaged over BENCH_N: born, goal loaded, goal run, disposed;
/// with BENCH_TICKS=1 the allocation ticks by type as well.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var count = Setting.Count(500);
        long born = 0, load = 0, run = 0, dispose = 0;
        ticks.@this? sampled = null;
        for (var i = -20; i < count; i++)
        {
            if (i == 0 && Setting.Ticks) sampled = new();
            var lap = new meter.@this();
            var app = Born.Request(); var made = lap.Lap();
            var goal = new Goal(app, Setting.Goal);
            var loaded = await goal.Load(); var read = lap.Lap();
            await goal.Start(loaded); var started = lap.Lap();
            await app.DisposeAsync(); var disposed = lap.Lap();
            if (i < 0) continue;
            born += made; load += read; run += started; dispose += disposed;
        }
        Say($"phases {Setting.Kind} born={born / count / 1024.0:F0} load={load / count / 1024.0:F0} run={run / count / 1024.0:F0} dispose={dispose / count / 1024.0:F0} KB");
        sampled?.Report(count);
        return 0;
    }
}
