using System.Diagnostics;
using System.Text;
using app.error;
using app.test;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.action.test;

/// <summary>
/// Main test-runner loop. C# handler — NOT a PLang foreach, immune to the silent-skip
/// bug that motivated this module. For each Ready global::app.module.action.test.test, spins up a fresh App
/// instance (file boundary = App boundary), subscribes AfterAction for coverage,
/// starts the test's entry goal under a timeout CancellationToken, records the test's outcome,
/// merges the child's Coverage into the parent, then releases the App.
/// Parallel execution is throttled by a SemaphoreSlim (Parallel / Testing.Parallel).
/// Never throws for child-test failures — failure is data, loop stays parallel-safe.
/// Returns the run-wide Results collection as Data so MetaTests can propagate explicitly
/// via `write to %results%`; child TestRuns do NOT auto-bubble to the parent runner.
/// </summary>
[Action("start", Cacheable = false)]
public partial class start : IContext
{
    // Test hook: fires once per child App after it's constructed and configured
    // (OsDirectory inherited, Test active, Current assigned),
    // before the test's entry goal runs. Tests attach probes here to snapshot
    // observable child-App state (parallel count, OsDirectory, Test presence).
    // Subscribers must be thread-safe — parallel tests fire this concurrently.
    internal static event Action<app.@this>? ChildAppCreated;

    [IsNotNull]
    public partial data.@this<global::app.type.item.list.@this<global::app.test.@this>> Tests { get; init; }

    public partial data.@this<global::app.type.item.number.@this>? Parallel { get; init; }
    public partial data.@this<global::app.type.item.number.@this>? Timeout { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.test.@this>>> Start()
    {
        var tests = new List<global::app.test.@this>();
        var list = await Tests.Value();
        if (list != null)
            foreach (var row in list.Items(Context))
                if (await row.Value() is global::app.test.@this test) tests.Add(test);
        var parentApp = Context.App;
        // The number lowers itself — absent slot falls to the stated default.
        var setting = Context.Setting.Of<global::app.test.setting.@this>();
        int parallel = Parallel == null ? setting.Parallel.ToInt32()
            : (await Parallel.Value())?.ToInt32() ?? setting.Parallel.ToInt32();
        double timeoutSeconds = Timeout == null ? setting.TimeoutSeconds.ToDouble()
            : (await Timeout.Value())?.ToDouble() ?? setting.TimeoutSeconds.ToDouble();
        // Sentinel: ≤ 0 means no timeout (the type enforces no bound; the consumer reads intent).
        var timeout = timeoutSeconds <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(timeoutSeconds);

        // The executed tests ARE the result — each carries its own outcome after run.
        // The list holds the test objects the run loop mutates in place.
        var executed = new global::app.type.item.list.@this<global::app.test.@this>(tests);
        if (tests.Count == 0)
            return Context.Ok<global::app.type.item.list.@this<global::app.test.@this>>(executed);

        // Sentinel: ≤ 0 means auto — fall back to the machine's processor count.
        if (parallel < 1) parallel = System.Environment.ProcessorCount;

        using var semaphore = new SemaphoreSlim(parallel);
        var tasks = tests.Select(async test =>
        {
            await semaphore.WaitAsync(Context.CancellationToken);
            try { await RunSingleAsync(test, timeout, parentApp); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        return Context.Ok<global::app.type.item.list.@this<global::app.test.@this>>(executed);
    }

    private async Task RunSingleAsync(global::app.test.@this test, TimeSpan timeout, app.@this parentApp)
    {
        // Non-ready tests (Stale, Skipped, etc.) are recorded but not executed —
        // the report surfaces them with their discovery-time status. Hiding them
        // would hurt CI visibility.
        if (test.Status != global::app.test.Status.Ready)
        {
            parentApp.test.list.Add(test);
            return;
        }

        // Child App roots at the PARENT'S root — same convention the .pr
        // files were built under. Root-relative paths (Goal.Path, GoalCall
        // PrPath, test.Goal.Path / PrPath — all "/Modules/..." shaped) then
        // resolve correctly. Rooting at test.Directory would deepen the
        // anchor and double-prefix every stored root-relative path.
        await using var childApp = new app.@this(parentApp.AbsolutePath);
        childApp.OsDirectory = parentApp.OsDirectory;
        childApp.Parent = parentApp;
        // The test's session: the child App is testing while it is open, and every write out of the
        // test lands in it (registered as the child's output).
        var session = childApp.test.list.Open(test);
        var coverage = childApp.test.list.Report.Coverage;

        test.Begin();

        // Coverage subscriber — records every handler fire and every branch index observed.
        // Site key for branches = "goalName:stepIndex"; matches what the report renders.
        var coverageBinding = new EventBinding(
            app.@event.Trigger.AfterAction,
            async (context, action, result) =>
            {
                if (action != null)
                {
                    coverage.RecordModuleAction(action.Module.Name, action.Name);

                    // Coverage DERIVES from the natural facts — the runtime stamps nothing. A condition
                    // that fired (its own result is truthy) records the branch it took, keyed by its
                    // position in the step's action chain (survives Merge — no object refs).
                    if (action.IsCondition && result != null && await result.ToBooleanAsync())
                    {
                        var goal = action.Step?.Goal;
                        var goalId = goal?.Path?.ToString() ?? goal?.Name ?? "?";
                        var stepIndex = action.Step?.Index.ToString() ?? "?";
                        var site = $"{goalId}:{stepIndex}";
                        var branchIdx = action.Step != null ? action.Step.Code.IndexOf(action) : -1;
                        coverage.RecordBranch(site, branchIdx);
                        coverage.RecordBranchLabel(site, action.Name);
                    }
                }
                return context.Ok();
            },
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(coverageBinding);

        // Per-step timing — only top-level steps of the entry goal. Nested
        // sub-goal steps roll up because the caller's AfterStep doesn't
        // fire until the sub-goal returns.
        var stepStarts = new Dictionary<int, long>();
        var entryGoalPath = test.Goal.Path?.ToString();
        bool IsEntryGoalStep(global::app.goal.step.@this step)
            => string.Equals(step.Goal.Path?.ToString(), entryGoalPath, StringComparison.Ordinal);

        var beforeStepBinding = new EventBinding(
            app.@event.Trigger.BeforeStep,
            (context, _, _) =>
            {
                if (context.Step is { } step && IsEntryGoalStep(step))
                    stepStarts[step.Index] = Stopwatch.GetTimestamp();
                return Task.FromResult(context.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false);
        var afterStepBinding = new EventBinding(
            app.@event.Trigger.AfterStep,
            (context, _, _) =>
            {
                if (context.Step is { } step && IsEntryGoalStep(step) && stepStarts.Remove(step.Index, out var start))
                {
                    var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    test.Timings.Add(new global::app.test.timing.@this
                    {
                        Step = step,
                        Elapsed = TimeSpan.FromMilliseconds(ms)
                    });
                }
                return Task.FromResult(context.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(beforeStepBinding);
        childApp.User.Context.Events.Register(afterStepBinding);

        // Test-only hook — see ChildAppCreated declaration above.
        ChildAppCreated?.Invoke(childApp);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.CancellationToken);
        cts.CancelAfter(timeout);

        // Bind cts to the child context's cancellation stack so downstream
        // handlers reading Context.CancellationToken (timer.sleep, http.request,
        // …) honour the per-test timeout. Same mechanism timeout.after uses.
        childApp.User.Context.PushCancellation(cts);

        try
        {
            // Goal.PrPath is derived from Goal.Path, anchored at the App root the child shares. The
            // child App loads it through its own goal collection, which resolves where it reads.
            var child = childApp.User.Context;
            var loaded = await childApp.goal.list.Load(test.Goal.PrPath!.ToString());
            var result = loaded.Success
                ? await ((await loaded.Value()) as global::app.goal.@this)!.Start(child)
                : loaded;
            if (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
                test.Complete(global::app.test.Status.Timeout);
            else
                test.Complete(result);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
        {
            test.Complete(global::app.test.Status.Timeout);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            test.Complete(global::app.test.Status.Fail,
                new ServiceError(ex.Message, "TestRunError", 500) { Exception = ex });
        }
        finally
        {
            childApp.User.Context.PopCancellation();
            test.Stdout = session.Text;
        }

        parentApp.test.list.Report.Coverage.Merge(coverage);
        parentApp.test.list.Add(test);
    }
}
