using Goal = Bench.goal.@this;

namespace Bench.mode.loadtypes;

/// <summary>Allocation ticks by type of an app born, and with BENCH_LOAD=1 its goal loaded: run both and subtract.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var count = Setting.Count(3000);
        for (var i = 0; i < 20; i++) await One();
        using var sampled = new ticks.@this();
        for (var i = 0; i < count; i++) await One();
        sampled.Report(count);
        return 0;
    }

    private async Task One()
    {
        await using var app = Born.Request();
        if (Setting.Load) await new Goal(app, Setting.Goal).Load();
    }
}
