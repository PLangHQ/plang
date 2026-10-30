using Goal = Bench.goal.@this;

namespace Bench.mode.events;

/// <summary>The bytes of an actor's events' first ask (the system events load), and of the goal's load and run after, per
/// app (this thread).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        var count = Setting.Count(500);
        long made = 0, loadSystem = 0, loadUser = 0, loaded = 0, run = 0;
        for (var i = 0; i < 20 + count; i++)
        {
            var lap = new meter.@this();
            await using var app = Born.Request();
            var born = lap.Lap();
            await app.actor.list.System.@event.Loaded(); var system = lap.Lap();
            await app.actor.list.User.@event.Loaded(); var user = lap.Lap();
            var goal = new Goal(app, Setting.Goal);
            var read = await goal.Load(); var load = lap.Lap();
            await goal.Start(read); var start = lap.Lap();
            if (i >= 20) { made += born; loadSystem += system; loadUser += user; loaded += load; run += start; }
        }
        Say($"app={made / count / 1024.0:F1} KB  system.load={loadSystem / count / 1024.0:F1} KB  user.load={loadUser / count / 1024.0:F1} KB  goal.load={loaded / count / 1024.0:F1} KB  goal.run={run / count / 1024.0:F1} KB");
        return 0;
    }
}
