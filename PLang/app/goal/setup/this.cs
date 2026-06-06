using app.actor.context;
using app.error;
using app.variable;

namespace app.goal.setup;

/// <summary>
/// Run-once setup execution system.
/// Setup goals execute once-per-step at app startup. Steps are tracked
/// persistently in the "setup" table of app.System.DataSource (system.sqlite),
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
    public IEnumerable<goal.@this> Goals => _goals.AllIncludingSetup
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
        var context = app.System.Context!;
        var candidates = new global::app.type.path.@this[]
        {
            global::app.type.path.@this.Resolve("/.build/setup.pr", context),
            global::app.type.path.@this.Resolve("/Setup/.build/setup.pr", context),
        };

        foreach (var file in candidates)
        {
            // ExistsAsync routes through AuthGate(Read) — in-root setup probes
            // fast-pass via IsInRoot. Out-of-root would prompt, but setup paths
            // are derived from App.AbsolutePath so this is always in-root.
            var exists = await file.ExistsAsync();
            if (!exists.Success || exists.Value != true) continue;

            try
            {
                // ReadText already MIME-deserializes .pr → Goal via the
                // FilePath.ReadText path. The per-Actor serializer carries a
                // Context-bound PathJsonConverter so Path fields land wired.
                var read = await file.ReadText();
                if (!read.Success || read.Value is not goal.@this goal || !goal.IsSetup) continue;

                foreach (var step in goal.Steps)
                {
                    step.Goal = goal;
                    foreach (var action in step.Actions)
                        action.Synthetic = false;
                }

                _goals.Add(goal);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                // Skip unparseable files — they'll fail when lazy-loaded later
            }
        }

        return data.@this.Ok();
    }

    /// <summary>
    /// Runs all setup goals. Sets context.Setup for the duration so Steps.RunAsync
    /// can check run-once semantics. Any goal called from within setup execution
    /// inherits the setup context (context.Setup propagates through goal.call).
    /// </summary>
    public async Task<data.@this> RunAsync(app.@this app, actor.context.@this context, CancellationToken ct = default)
    {
        var discoverResult = await DiscoverAsync(app, ct);
        if (!discoverResult.Success) return discoverResult;

        if (!Goals.Any()) return data.@this.Ok();

        context.Setup = this;
        try
        {
            foreach (var goal in Goals)
            {
                var result = await app.RunGoalAsync(goal, context, ct);
                if (!result.Success) return result;
            }
            return data.@this.Ok();
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

        var result = await app.SettingsStore.Exists(Table, step.Hash);
        return result.Success && result.Value;
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
    public async Task<data.@this> Record(Step step, app.@this app, IError? error = null)
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

        return await app.SettingsStore.Set(Table, step.Hash, new data.@this(step.Hash, metadata));
    }
}
