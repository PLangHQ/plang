using app.actor.context;
using app.error;

namespace app.goal.setup;

/// <summary>
/// Run-once setup execution system.
/// Setup goals execute once-per-step at app startup. Steps are tracked
/// persistently in the "setup" table of app.actor.list.System.DataSource (system.sqlite),
/// keyed by step.Hash. New steps run on next startup. Changed steps (different hash) re-run.
/// </summary>
public sealed class @this
{
    private readonly global::app.goal.list.@this _goals;
    private const string Table = "setup";

    public @this(global::app.goal.list.@this goals)
    {
        _goals = goals;
    }

    /// <summary>
    /// Setup goals, ordered: goal named "Setup" first, then alphabetical.
    /// </summary>
    public IEnumerable<goal.@this> Goals => _goals.Items()
        .Where(g => g.IsSetup)
        .OrderBy(g => g.Name.Equals("Setup", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Discovers setup goals by convention:
    /// 1. Root .build/setup.pr (the app's main Setup.goal)
    /// 2. Setup/.build/setup.pr (a dedicated Setup/ folder)
    /// Does NOT scan all .pr files — there could be thousands.
    /// </summary>
    private async Task<data.@this> DiscoverAsync(app.@this app, CancellationToken ct = default)
    {
        var context = app.actor.list.System.Context!;
        var candidates = new global::app.type.item.path.@this[]
        {
            global::app.type.item.path.@this.Resolve("/.build/setup.pr", context),
            global::app.type.item.path.@this.Resolve("/Setup/.build/setup.pr", context),
        };

        foreach (var file in candidates)
        {
            // ExistsAsync routes through AuthGate(Read) — in-root setup probes
            // fast-pass via IsInRoot. Out-of-root would prompt, but setup paths
            // are derived from App.AbsolutePath so this is always in-root.
            var exists = await file.ExistsAsync(context);
            if (!exists.Success) return exists;
            if ((await exists.Value())?.Value != true) continue;

            // The .pr lands as a file reference whose value its format decodes into a goal. A setup file
            // that doesn't read or parse is the answer — setup doesn't run past it.
            var read = await file.Read(context);
            var content = read.Success ? await read.Value() : null;
            if (!read.Success) return read;
            if (content as global::app.goal.@this is not { } goal || !goal.IsSetup) continue;

            _goals.Add(goal);
        }

        return context.Ok();
    }

    /// <summary>
    /// Starts all setup goals. Sets context.Setup for the duration so the steps
    /// can check run-once semantics. Any goal called from within setup execution
    /// inherits the setup context (context.Setup propagates through goal.call).
    /// </summary>
    public async Task<data.@this> Start(app.@this app, actor.context.@this context, CancellationToken ct = default)
    {
        var discoverResult = await DiscoverAsync(app, ct);
        if (!discoverResult.Success) return discoverResult;

        if (!Goals.Any()) return context.Ok();

        context.Setup = this;
        try
        {
            foreach (var goal in Goals)
            {
                var result = await app.Start(goal, context, ct);
                if (!result.Success) return result;
            }
            return context.Ok();
        }
        finally
        {
            context.Setup = null;
        }
    }

    /// <summary>
    /// Checks if a step has already been executed (by hash lookup in system DataSource).
    /// </summary>
    public async Task<bool> IsExecuted(Step step, app.@this app)
    {
        if (string.IsNullOrEmpty(step.Hash)) return false;

        var result = await app.store.Exists(Table, step.Hash);
        return result.Success && await result.ToBooleanAsync();
    }

    /// <summary>
    /// Checks if a step error is automatically tolerable during setup.
    /// Matches runtime1 behavior: "already exists" (table/index) and "duplicate column name"
    /// are expected in idempotent setup re-runs and should not abort.
    /// </summary>
    public bool IsTolerableError(data.@this result)
    {
        if (result.Success) return false;
        var message = result.Error?.Message;
        if (string.IsNullOrEmpty(message)) return false;
        return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Records a step execution in the system DataSource.
    /// Returns Data so the caller can detect recording failures.
    /// </summary>
    public async Task<data.@this> Record(Step step, app.@this app, global::app.error.Error? error = null)
    {
        if (string.IsNullOrEmpty(step.Hash)) return data.@this.Ok();

        var metadata = new Dictionary<string, object?>
        {
            ["goalPath"] = step.Goal?.Path,
            ["stepIndex"] = step.Index,
            ["stepText"] = step.Text,
            ["executedAt"] = DateTime.UtcNow.ToString("O"),
            ["error"] = error?.Message
        };

        return await app.store.Set(Table, step.Hash, new data.@this(step.Hash, metadata, context: app.actor.list.System.Context));
    }
}
