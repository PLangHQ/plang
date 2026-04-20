using App.Errors;
using App.Test;
using App.Variables;
using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.modules.test;

/// <summary>
/// Main test-runner loop. C# handler — NOT a PLang foreach, immune to the silent-skip
/// bug that motivated this module. For each Ready TestFile, spins up a fresh App
/// instance (file boundary = App boundary), subscribes AfterAction for coverage,
/// runs the test's entry goal under a timeout CancellationToken, records a TestRun,
/// merges the child's Coverage into the parent, then releases the App.
/// Parallel execution is throttled by a SemaphoreSlim (Parallel / Testing.Parallel).
/// Never throws for child-test failures — failure is data, loop stays parallel-safe.
/// Returns the run-wide Results collection as Data so MetaTests can propagate explicitly
/// via `write to %results%`; child TestRuns do NOT auto-bubble to the parent runner.
/// </summary>
[Example("run tests %tests%, write to %results%",
    "Tests=%tests%")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    [IsNotNull]
    public partial Data.@this<List<TestFile>> Tests { get; init; }

    public partial Data.@this<int>? Parallel { get; init; }
    public partial Data.@this<int>? Timeout { get; init; }

    public async Task<Data.@this> Run()
    {
        var tests = Tests.Value ?? new List<TestFile>();
        var parentApp = Context.App!;
        var parallel = Parallel?.Value ?? parentApp.Testing.Parallel;
        var timeoutSeconds = Timeout?.Value ?? parentApp.Testing.TimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        if (tests.Count == 0)
            return App.Data.@this.Ok(parentApp.Testing.Results);

        if (parallel < 1) parallel = 1;

        using var semaphore = new SemaphoreSlim(parallel);
        var tasks = tests.Select(async test =>
        {
            await semaphore.WaitAsync(Context.CancellationToken);
            try { await RunSingleAsync(test, timeout, parentApp); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        return App.Data.@this.Ok(parentApp.Testing.Results);
    }

    private async Task RunSingleAsync(TestFile test, TimeSpan timeout, App.@this parentApp)
    {
        // Non-ready tests (Stale, Skipped, etc.) are recorded but not executed —
        // the report surfaces them with their discovery-time status. Hiding them
        // would hurt CI visibility.
        if (test.Status != TestStatus.Ready)
        {
            var skipRun = new TestRun(test);
            skipRun.Complete(test.Status, skipRun.Error);
            parentApp.Testing.Results.Add(skipRun);
            return;
        }

        await using var childApp = new App.@this(test.Directory);
        childApp.SystemDirectory = parentApp.SystemDirectory;
        childApp.Testing.IsEnabled = true;
        var testRun = new TestRun(test);
        childApp.Testing.CurrentTest = testRun;

        // Coverage subscriber — records every handler fire and every branch index observed.
        // Site key for branches = "goalName:stepIndex"; matches what the report renders.
        var coverageBinding = new EventBinding(
            App.Events.EventType.AfterAction,
            (ctx, action, result) =>
            {
                if (action != null)
                {
                    childApp.Testing.Coverage.RecordModuleAction(action.Module, action.ActionName);

                    if (string.Equals(action.Module, "condition", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(action.ActionName, "if", StringComparison.OrdinalIgnoreCase)
                        && result != null && result.Properties.Contains("branchIndex")
                        // Ignore inner-elseif simple-path firings — they'd mix
                        // {true, elseif[1]} labels into the orchestrator's chain.
                        && App.modules.condition.BranchChain.IsFirstConditionInStep(action))
                    {
                        // Site key carries the goal's source file so sites from
                        // different files don't collide on shared names like "Start".
                        var goal = action.Step?.Goal;
                        var goalId = goal?.Path ?? goal?.Name ?? "?";
                        var stepIndex = action.Step?.Index.ToString() ?? "?";
                        var site = $"{goalId}:{stepIndex}";
                        childApp.Testing.Coverage.RecordBranch(site, result.Properties.Get<int>("branchIndex"));
                        if (result.Properties.Contains("branchLabel"))
                        {
                            var label = result.Properties.Get<string>("branchLabel");
                            if (!string.IsNullOrEmpty(label))
                                childApp.Testing.Coverage.RecordBranchLabel(site, label);
                        }
                        if (result.Properties.Contains("branchChain"))
                        {
                            var chain = result.Properties["branchChain"]?.Value as List<string>;
                            if (chain != null)
                                childApp.Testing.Coverage.RecordBranchChain(site, chain);
                        }
                    }
                }
                return Task.FromResult(App.Data.@this.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(coverageBinding);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.CancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var goalCall = new Goals.Goal.GoalCall { PrPath = test.PrPath };
            var result = await childApp.RunGoalAsync(goalCall, childApp.User.Context, cts.Token);
            if (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
                testRun.Complete(TestStatus.Timeout);
            else
                testRun.Complete(result);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
        {
            testRun.Complete(TestStatus.Timeout);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            testRun.Complete(TestStatus.Fail,
                new ServiceError(ex.Message, "TestRunError", 500) { Exception = ex });
        }

        // Accumulate UserTags added via test.tag during the run.
        foreach (var tag in childApp.Testing.CurrentTest?.UserTags ?? Enumerable.Empty<string>())
            testRun.UserTags.Add(tag);

        parentApp.Testing.Coverage.Merge(childApp.Testing.Coverage);
        parentApp.Testing.Results.Add(testRun);
    }
}
