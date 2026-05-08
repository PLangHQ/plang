using App.Actor.Context;
using App.Errors;
using App.Variables;

namespace App.Goals.Setup;

/// <summary>
/// Run-once setup execution system.
/// Setup goals execute once-per-step at app startup. Steps are tracked
/// persistently in the "setup" table of app.System.DataSource (system.sqlite),
/// keyed by step.Hash. New steps run on next startup. Changed steps (different hash) re-run.
/// </summary>
public sealed class @this
{
    private readonly AppGoals _goals;
    private const string Table = "setup";

    public @this(AppGoals goals)
    {
        _goals = goals;
    }

    /// <summary>
    /// Setup goals, ordered: goal named "Setup" first, then alphabetical.
    /// </summary>
    public IEnumerable<Goal.@this> Goals => _goals.AllIncludingSetup
        .Where(g => g.IsSetup)
        .OrderBy(g => g.Name.Equals("Setup", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Discovers setup goals by convention:
    /// 1. Root .build/setup.pr (the app's main Setup.goal)
    /// 2. Setup/.build/setup.pr (a dedicated Setup/ folder)
    /// Does NOT scan all .pr files — there could be thousands.
    /// </summary>
    private async Task<Data.@this> DiscoverAsync(App.@this app, CancellationToken ct = default)
    {
        var fs = app.FileSystem;
        var root = app.AbsolutePath;

        var candidates = new[]
        {
            fs.Path.Combine(root, ".build", "setup.pr"),
            fs.Path.Combine(root, "Setup", ".build", "setup.pr"),
        };

        foreach (var file in candidates)
        {
            if (!fs.File.Exists(file)) continue;

            try
            {
                var content = await fs.File.ReadAllTextAsync(file, ct);
                var ext = fs.Path.GetExtension(file);
                var goal = app.System.Channels.Serializers.Deserialize<Goal.@this>(new App.Channels.Serializers.DeserializeOptions { Value = content, Extension = ext });
                if (goal == null || !goal.IsSetup) continue;

                foreach (var step in goal.Steps)
                    step.Goal = goal;

                _goals.Add(goal);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                // Skip unparseable files — they'll fail when lazy-loaded later
            }
        }

        return Data.@this.Ok();
    }

    /// <summary>
    /// Runs all setup goals. Sets context.Setup for the duration so Steps.RunAsync
    /// can check run-once semantics. Any goal called from within setup execution
    /// inherits the setup context (context.Setup propagates through goal.call).
    /// </summary>
    public async Task<Data.@this> RunAsync(App.@this app, Actor.Context.@this context, CancellationToken ct = default)
    {
        var discoverResult = await DiscoverAsync(app, ct);
        if (!discoverResult.Success) return discoverResult;

        if (!Goals.Any()) return Data.@this.Ok();

        context.Setup = this;
        try
        {
            foreach (var goal in Goals)
            {
                var result = await app.RunGoalAsync(goal, context, ct);
                if (!result.Success) return result;
            }
            return Data.@this.Ok();
        }
        finally
        {
            context.Setup = null;
        }
    }

    /// <summary>
    /// Checks if a step has already been executed (by hash lookup in system DataSource).
    /// </summary>
    public async Task<bool> IsExecuted(Step step, App.@this app)
    {
        if (string.IsNullOrEmpty(step.Hash)) return false;

        var result = await app.SettingsStore.Exists(Table, step.Hash);
        return result.Success && result.Value is true;
    }

    /// <summary>
    /// Checks if a step error is automatically tolerable during setup.
    /// Matches runtime1 behavior: "already exists" (table/index) and "duplicate column name"
    /// are expected in idempotent setup re-runs and should not abort.
    /// </summary>
    public bool IsTolerableError(Data.@this result)
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
    public async Task<Data.@this> Record(Step step, App.@this app, IError? error = null)
    {
        if (string.IsNullOrEmpty(step.Hash)) return Data.@this.Ok();

        var metadata = new Dictionary<string, object?>
        {
            ["goalPath"] = step.Goal?.Path,
            ["stepIndex"] = step.Index,
            ["stepText"] = step.Text,
            ["executedAt"] = DateTime.UtcNow.ToString("O"),
            ["error"] = error?.Message
        };

        return await app.SettingsStore.Set(Table, step.Hash, new Data.@this(step.Hash, metadata));
    }
}
