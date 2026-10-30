namespace Bench.mode.resolve;

/// <summary>What <c>shell.Resolve</c> of a <c>set %x% = "hello"</c> action is made of: the handler's constructor, the
/// setting lookup for a parameter the step did not give, the action's address, and the whole Resolve; the best of 7
/// rounds each (a loaded machine inflates a round, never shortens one).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        await using var app = Born.Request();
        var context = app.actor.list.User.Context;
        var goal = (global::app.goal.@this)(await (await app.goal.Load("/app/stepresult/StepResult.test.goal")).Value())!;
        var action = goal.Step.Items().First().Code.Items().First();
        var shell = action.Instance(context).Code!;
        await Best("shell.Resolve (whole)", async () => _ = await shell.Resolve(action, context));
        await Best("new Set(context) — one handler's ctor", () => { _ = new global::app.module.variable.Set(context); return Task.CompletedTask; });
        await Best("Module.Create (the shell)", () => { _ = action.Module.Create(action.Name, context); return Task.CompletedTask; });
        await Best("Setting.Get(action, absent option)", async () => _ = await context.Setting.Get(action, "type"));
        await Best("action.Address", () => { _ = action.Address; return Task.CompletedTask; });
        await Best("action[\"name\"].Data(context) (a view)", () => { _ = action["name"]?.Data(context); return Task.CompletedTask; });
        return 0;
    }

    // The best of 7 rounds of 20,000 runs: microseconds and bytes (this thread).
    private async Task Best(string name, Func<Task> operation)
    {
        for (var i = 0; i < 2000; i++) await operation();
        double best = double.MaxValue, bytes = 0;
        for (var round = 0; round < 7; round++)
        {
            var lap = new meter.@this();
            var time = new meter.@this();
            for (var i = 0; i < 20000; i++) await operation();
            var us = time.Elapsed.TotalMilliseconds * 1000 / 20000;
            if (us < best) { best = us; bytes = lap.Lap() / 20000.0; }
        }
        Say($"{name,-46} {best,8:F2} us {bytes,8:F0} B");
    }
}
