using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Setup;

/// <summary>
/// Run-once setup execution system.
/// Setup goals execute once-per-step at app startup. Steps are tracked
/// persistently in the "setup" table of engine.System.DataSource (system.sqlite),
/// keyed by step.Hash. New steps run on next startup. Changed steps (different hash) re-run.
/// </summary>
public sealed class @this
{
    private readonly EngineGoals _goals;
    private const string Table = "setup";

    public @this(EngineGoals goals)
    {
        _goals = goals;
    }

    /// <summary>
    /// Setup goals, ordered: goal named "Setup" first, then alphabetical.
    /// </summary>
    public IEnumerable<Goal.@this> Goals => _goals.All
        .Where(g => g.IsSetup)
        .OrderBy(g => g.Name.Equals("Setup", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs all setup goals. Sets context.Setup for the duration so Steps.RunAsync
    /// can check run-once semantics. Any goal called from within setup execution
    /// inherits the setup context (context.Setup propagates through goal.call).
    /// </summary>
    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken ct = default)
    {
        context.Setup = this;
        try
        {
            foreach (var goal in Goals)
            {
                var loadResult = await goal.Load(context);
                if (!loadResult.Success) return loadResult;

                var result = await goal.RunAsync(engine, context, ct);
                if (!result.Success) return result;
            }
            return Data.Ok();
        }
        finally
        {
            context.Setup = null;
        }
    }

    /// <summary>
    /// Checks if a step has already been executed (by hash lookup in system DataSource).
    /// </summary>
    public async Task<bool> IsExecuted(Step step, Engine.@this engine)
    {
        if (string.IsNullOrEmpty(step.Hash)) return false;

        var result = await engine.System.DataSource.Exists(Table, step.Hash);
        return result.Success && result.Value is true;
    }

    /// <summary>
    /// Records a step execution in the system DataSource.
    /// Called after a step runs (even on tolerated errors).
    /// </summary>
    public async Task Record(Step step, Engine.@this engine, IError? error = null)
    {
        if (string.IsNullOrEmpty(step.Hash)) return;

        var metadata = new Dictionary<string, object?>
        {
            ["goalPath"] = step.Goal?.Path,
            ["stepIndex"] = step.Index,
            ["stepText"] = step.Text,
            ["executedAt"] = DateTime.UtcNow.ToString("O"),
            ["error"] = error?.Message
        };

        await engine.System.DataSource.Set(Table, step.Hash, metadata);
    }
}
