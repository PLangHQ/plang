namespace Bench.mode.birth;

/// <summary>An app's birth: 20 warm, then 100 timed, each disposed before the next; the bytes count the birth and its
/// dispose (a request app pays both).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        const int warm = 20, n = 100;
        for (var i = 0; i < warm; i++) await Born.Testing(Born.Make(true)).DisposeAsync();
        var births = new List<double>();
        var run = new meter.@this();
        for (var i = 0; i < n; i++)
        {
            var one = new meter.@this();
            var app = Born.Make(true);
            births.Add(one.Elapsed.TotalMilliseconds);
            await Born.Testing(app).DisposeAsync();
        }
        births.Sort();
        Say($"birth {Setting.Kind} ms={births[n / 2]:F2} min={births[0]:F2} p90={births[n * 9 / 10]:F2} cpu={run.Cpu.TotalMilliseconds / n:F2} kb={run.All / (double)n / 1024.0:F0}");
        return 0;
    }
}
