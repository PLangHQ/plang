namespace Bench.mode.copy;

/// <summary>What <c>data.Copy</c> costs — for a value that carries nothing (a goal-call argument, <c>set %y% = %x%</c>), for
/// one whose property bag was made but is empty, and for one that carries a property — in bytes (this thread) and
/// microseconds, best of 7 rounds.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        await using var app = Born.Request();
        var context = app.actor.list.User.Context;
        var plain = new global::app.data.@this("x", "hello", context: context);
        var emptied = new global::app.data.@this("x", "hello", context: context);
        _ = emptied.Properties.Count;                       // the bag made, nothing in it
        var carrying = new global::app.data.@this("x", "hello", context: context);
        carrying.Properties["note"] = "kept";   // a property carried
        Best("Copy(name) of a value that carries nothing", () => plain.Copy("y"));
        Best("Copy(name) of a value whose bag was made, empty", () => emptied.Copy("y"));
        Best("Copy(name) of a value carrying one property", () => carrying.Copy("y"));
        Best("Copy(context) (a goal-call argument)", () => plain.Copy(context));
        Best("new data(name, item) (what a copy builds, no bag)", () => new global::app.data.@this("y", "hello", context: context));
        return 0;
    }

    private void Best(string name, Func<object> make)
    {
        for (var i = 0; i < 20000; i++) make();
        double best = double.MaxValue, bytes = 0;
        for (var round = 0; round < 7; round++)
        {
            var lap = new meter.@this();
            var time = new meter.@this();
            for (var i = 0; i < 100000; i++) make();
            var us = time.Elapsed.TotalMilliseconds * 1000 / 100000;
            if (us < best) { best = us; bytes = lap.Lap() / 100000.0; }
        }
        Say($"{name,-56} {best,7:F3} us {bytes,6:F0} B");
    }
}
