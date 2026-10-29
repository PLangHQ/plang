namespace Bench.mode.index;

/// <summary>The bytes of one action.Index (a search of its step's code) over the actions of the request goal.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        await using var app = Born.Request();
        var loaded = await app.goal.Load(Setting.Goal);
        var goal = (global::app.goal.@this)(await loaded.Value())!;
        var actions = goal.Step.Items().SelectMany(step => step.Code.Items()).ToList();
        foreach (var action in actions) _ = action.Index;
        var lap = new meter.@this();
        for (var round = 0; round < 1000; round++) foreach (var action in actions) _ = action.Index;
        Say($"index actions={actions.Count} bytes-per-pass={lap.Lap() / 1000.0:F0}");
        return 0;
    }
}
