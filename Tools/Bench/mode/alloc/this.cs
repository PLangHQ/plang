namespace Bench.mode.alloc;

/// <summary>Allocation ticks (one per ~100 KB, sampled by bytes) by type over BENCH_N births: `type kb-per-birth`.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var count = Setting.Count(3000);
        for (var i = 0; i < 20; i++) await Born.Request().DisposeAsync();
        using var sampled = new ticks.@this();
        for (var i = 0; i < count; i++) await Born.Request().DisposeAsync();
        sampled.Report(count);
        return 0;
    }
}
