using Goal = Bench.goal.@this;

namespace Bench.mode.request;

/// <summary>A request app BENCH_N (3,000) times: born, a goal loaded and run, disposed; time and bytes each, and the
/// MB held after each thousand and after a rest — flat is no leak.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var total = Setting.Count(3000);
        const int block = 1000;
        var failed = 0;
        string? why = null;
        // the server is made before the first count: what it holds is the process's, not the requests'
        if (Setting.Child) _ = Born.Server;
        var held = new List<double> { heap.@this.Held() };
        var run = new meter.@this();
        double running = 0;
        for (var done = 0; done < total; done += block)
        {
            var thousand = new meter.@this();
            for (var i = 0; i < Math.Min(block, total - done); i++)
            {
                await using var app = Born.Request();
                var goal = new Goal(app, Setting.Goal);
                var result = await goal.Start(await goal.Load());
                if (!result.Success) { failed++; why ??= result.Error?.Message; }
            }
            running += thousand.Elapsed.TotalSeconds;
            held.Add(heap.@this.Held());
        }
        // a rest, then collect again: what a library lets go only on its own timer is let go now
        if (Setting.Rest > 0) { await Task.Delay(Setting.Rest * 1000); held.Add(heap.@this.Held()); }
        Say($"request {Setting.Kind} ms={running * 1000 / total:F2} kb={run.All / (double)total / 1024.0:F0} held={string.Join(">", held.Select(h => h.ToString("F1")))} failed={failed}{(why != null ? $" why={why.Replace(' ', '_')}" : "")}");
        return failed == 0 ? 0 : 1;
    }
}
