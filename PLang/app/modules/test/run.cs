using System.Diagnostics;
using System.Text;
using app.errors;
using app.tester;
using app.variables;
using EventBinding = app.events.lifecycle.bindings.binding.@this;

namespace app.modules.test;

/// <summary>
/// Main test-runner loop. C# handler — NOT a PLang foreach, immune to the silent-skip
/// bug that motivated this module. For each Ready global::app.tester.Test.@this, spins up a fresh App
/// instance (file boundary = App boundary), subscribes AfterAction for coverage,
/// runs the test's entry goal under a timeout CancellationToken, records a global::app.tester.Run,
/// merges the child's Coverage into the parent, then releases the App.
/// Parallel execution is throttled by a SemaphoreSlim (Parallel / Testing.Parallel).
/// Never throws for child-test failures — failure is data, loop stays parallel-safe.
/// Returns the run-wide Results collection as Data so MetaTests can propagate explicitly
/// via `write to %results%`; child TestRuns do NOT auto-bubble to the parent runner.
/// </summary>
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    // Test hook: fires once per child App after it's constructed and configured
    // (OsDirectory inherited, Testing.IsEnabled=true, CurrentTest assigned),
    // before the test's entry goal runs. Tests attach probes here to snapshot
    // observable child-App state (parallel count, OsDirectory, IsEnabled).
    // Subscribers must be thread-safe — parallel tests fire this concurrently.
    internal static event Action<app.@this>? ChildAppCreated;

    [IsNotNull]
    public partial data.@this<List<global::app.tester.Test.@this>> Tests { get; init; }

    public partial data.@this<int>? Parallel { get; init; }
    public partial data.@this<int>? Timeout { get; init; }

    public async Task<data.@this<global::app.tester.Results>> Run()
    {
        var tests = Tests.Value ?? new List<global::app.tester.Test.@this>();
        var parentApp = Context.App!;
        var parallel = Parallel?.Value ?? parentApp.Tester.Parallel;
        var timeoutSeconds = Timeout?.Value ?? parentApp.Tester.TimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        if (tests.Count == 0)
            return data.@this<global::app.tester.Results>.Ok(parentApp.Tester.Results);

        if (parallel < 1) parallel = 1;

        using var semaphore = new SemaphoreSlim(parallel);
        var tasks = tests.Select(async test =>
        {
            await semaphore.WaitAsync(Context.CancellationToken);
            try { await RunSingleAsync(test, timeout, parentApp); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        return data.@this<global::app.tester.Results>.Ok(parentApp.Tester.Results);
    }

    private async Task RunSingleAsync(global::app.tester.Test.@this test, TimeSpan timeout, app.@this parentApp)
    {
        // Non-ready tests (Stale, Skipped, etc.) are recorded but not executed —
        // the report surfaces them with their discovery-time status. Hiding them
        // would hurt CI visibility.
        if (test.Status != global::app.tester.Status.Ready)
        {
            var skipRun = new global::app.tester.Run(test);
            skipRun.Complete(test.Status, skipRun.Error);
            parentApp.Tester.Results.Add(skipRun);
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
        childApp.Tester.IsEnabled = true;

        // Freeze foundational channels NOW — before any user code (channel.set,
        // etc.) registers overlays. Without this, FoundationalChannels lazy-
        // snapshots on first read, which is AFTER user-installed goal channels;
        // a goal-channel answerer then finds itself as its own "input" and
        // recurses to stack overflow.
        childApp.System.FreezeFoundational();
        childApp.User.FreezeFoundational();
        var testRun = new global::app.tester.Run(test);
        childApp.Tester.CurrentTest = testRun;

        // Coverage subscriber — records every handler fire and every branch index observed.
        // Site key for branches = "goalName:stepIndex"; matches what the report renders.
        var coverageBinding = new EventBinding(
            app.events.EventType.AfterAction,
            (ctx, action, result) =>
            {
                if (action != null)
                {
                    childApp.Tester.Coverage.RecordModuleAction(action.Module, action.ActionName);

                    if (action.IsIfHead
                        && result != null && result.Properties.Contains("branchIndex"))
                    {
                        // Site key carries the goal's source file so sites from
                        // different files don't collide on shared names like "Start".
                        var goal = action.Step?.Goal;
                        var goalId = goal?.Path?.ToString() ?? goal?.Name ?? "?";
                        var stepIndex = action.Step?.Index.ToString() ?? "?";
                        var site = $"{goalId}:{stepIndex}";
                        childApp.Tester.Coverage.RecordBranch(site, result.Properties.Get<int>("branchIndex"));
                        if (result.Properties.Contains("branchLabel"))
                        {
                            var label = result.Properties.Get<string>("branchLabel");
                            if (!string.IsNullOrEmpty(label))
                                childApp.Tester.Coverage.RecordBranchLabel(site, label);
                        }
                        if (result.Properties.Contains("branchChain"))
                        {
                            var chain = result.Properties["branchChain"] as List<string>;
                            if (chain != null)
                                childApp.Tester.Coverage.RecordBranchChain(site, chain);
                        }
                    }
                }
                return Task.FromResult(app.data.@this.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(coverageBinding);

        // Output capture — every write on the User actor's "output" channel
        // appends to testRun.Output. BeforeWrite (not AfterWrite) — AfterWrite
        // fires with the *result* of WriteCore (typically value-less Ok), while
        // BeforeWrite fires with the *input* envelope carrying the actual
        // payload. Filtered by channel name so writes to "error" / "debug"
        // don't get captured.
        var outputBuf = new StringBuilder();
        var outputBinding = new EventBinding(
            app.events.EventType.BeforeWrite,
            (ctx, _, written) =>
            {
                // Append newline per write — the stream-channel's text serializer
                // adds one on flush, so this matches "what stdout sees". A
                // payload that already ends in \n just gets a blank line, which
                // is still readable in the UI.
                if (written?.Value != null)
                    outputBuf.Append(written.Value).Append('\n');
                return Task.FromResult(app.data.@this.Ok());
            },
            channelName: app.channels.@this.Output,
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(outputBinding);

        // Per-step timing — only top-level steps of the entry goal. Nested
        // sub-goal steps roll up because the caller's AfterStep doesn't
        // fire until the sub-goal returns.
        var stepStarts = new Dictionary<int, long>();
        var entryGoalPath = test.Goal.Path?.ToString();
        bool IsEntryGoalStep(global::app.goals.goal.steps.step.@this? step)
            => step != null
            && string.Equals(step.Goal?.Path?.ToString(), entryGoalPath, StringComparison.Ordinal);

        var beforeStepBinding = new EventBinding(
            app.events.EventType.BeforeStep,
            (ctx, _, _) =>
            {
                var step = ctx.Step;
                if (IsEntryGoalStep(step))
                    stepStarts[step!.Index] = Stopwatch.GetTimestamp();
                return Task.FromResult(app.data.@this.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false);
        var afterStepBinding = new EventBinding(
            app.events.EventType.AfterStep,
            (ctx, _, _) =>
            {
                var step = ctx.Step;
                if (IsEntryGoalStep(step) && stepStarts.Remove(step!.Index, out var start))
                {
                    var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    testRun.Timings.Add(step.Index, ms);
                }
                return Task.FromResult(app.data.@this.Ok());
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
            // Goal.PrPath is derived from Goal.Path — already a path.@this
            // anchored at the child App's root (same root as the parent, post
            // path-canonicalization on this branch).
            var goalCall = new GoalCall { PrPath = test.Goal.PrPath };
            var result = await childApp.RunGoalAsync(goalCall, childApp.User.Context, cts.Token);
            if (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
                testRun.Complete(global::app.tester.Status.Timeout);
            else
                testRun.Complete(result);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
        {
            testRun.Complete(global::app.tester.Status.Timeout);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            testRun.Complete(global::app.tester.Status.Fail,
                new ServiceError(ex.Message, "TestRunError", 500) { Exception = ex });
        }
        finally
        {
            childApp.User.Context.PopCancellation();
            testRun.Output = outputBuf.Length > 0 ? outputBuf.ToString() : null;
        }

        parentApp.Tester.Coverage.Merge(childApp.Tester.Coverage);
        parentApp.Tester.Results.Add(testRun);
    }
}
