using System.Diagnostics;
using System.Text;
using app.error;
using app.test;
using app.variable;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.test;

/// <summary>
/// Main test-runner loop. C# handler — NOT a PLang foreach, immune to the silent-skip
/// bug that motivated this module. For each Ready global::app.module.test.test, spins up a fresh App
/// instance (file boundary = App boundary), subscribes AfterAction for coverage,
/// runs the test's entry goal under a timeout CancellationToken, records a global::app.test.Run,
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
    // (OsDirectory inherited, Test active, Current assigned),
    // before the test's entry goal runs. Tests attach probes here to snapshot
    // observable child-App state (parallel count, OsDirectory, Test presence).
    // Subscribers must be thread-safe — parallel tests fire this concurrently.
    internal static event Action<app.@this>? ChildAppCreated;

    [IsNotNull]
    public partial data.@this<global::app.type.item.list.@this<global::app.test.@this>> Tests { get; init; }

    public partial data.@this<global::app.type.item.number.@this>? Parallel { get; init; }
    public partial data.@this<global::app.type.item.number.@this>? Timeout { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.test.@this>>> Run()
    {
        var tests = new List<global::app.test.@this>();
        var list = await Tests.Value();
        if (list != null)
            foreach (var row in list)
                if (await row.Value() is global::app.test.@this test) tests.Add(test);
        var parentApp = Context.App;
        // The number lowers itself — absent slot falls to the stated default.
        int parallel = Parallel == null ? parentApp.Test.Parallel.ToInt32()
            : (await Parallel.Value())?.ToInt32() ?? parentApp.Test.Parallel.ToInt32();
        double timeoutSeconds = Timeout == null ? parentApp.Test.TimeoutSeconds.ToDouble()
            : (await Timeout.Value())?.ToDouble() ?? parentApp.Test.TimeoutSeconds.ToDouble();
        // Sentinel: ≤ 0 means no timeout (the type enforces no bound; the consumer reads intent).
        var timeout = timeoutSeconds <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(timeoutSeconds);

        // The executed tests ARE the result — each carries its own outcome after run.
        // The list holds the test objects the run loop mutates in place.
        var executed = new global::app.type.item.list.@this<global::app.test.@this>(tests, Context);
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
            parentApp.Test.Add(test);
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
        childApp.Test = new global::app.test.list.@this(childApp.System.Context);

        test.Start();
        childApp.Test.Current = test;

        // Coverage subscriber — records every handler fire and every branch index observed.
        // Site key for branches = "goalName:stepIndex"; matches what the report renders.
        var coverageBinding = new EventBinding(
            app.@event.Trigger.AfterAction,
            async (context, action, result) =>
            {
                if (action != null)
                {
                    childApp.Test.Coverage.RecordModuleAction(action.Module, action.ActionName);

                    if (action.IsIfHead
                        && result != null && result.Properties.Contains("branchIndex"))
                    {
                        // Site key carries the goal's source file so sites from
                        // different files don't collide on shared names like "Start".
                        var goal = action.Step?.Goal;
                        var goalId = goal?.Path?.ToString() ?? goal?.Name ?? "?";
                        var stepIndex = action.Step?.Index.ToString() ?? "?";
                        var site = $"{goalId}:{stepIndex}";
                        childApp.Test.Coverage.RecordBranch(site, await result.Properties.Get<int>("branchIndex"));
                        if (result.Properties.Contains("branchLabel"))
                        {
                            var label = await result.Properties.Get<string>("branchLabel");
                            if (!string.IsNullOrEmpty(label))
                                childApp.Test.Coverage.RecordBranchLabel(site, label);
                        }
                        if (result.Properties.Contains("branchChain"))
                        {
                            var chain = await result.Properties.Get<List<string>>("branchChain");
                            if (chain != null)
                                childApp.Test.Coverage.RecordBranchChain(site, chain);
                        }
                    }
                }
                return context.Ok();
            },
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(coverageBinding);

        // Output capture — every write on the User actor's "output" channel
        // appends to testRun.Output. BeforeWrite (not AfterWrite) — AfterWrite
        // fires with the *result* of WriteCore (typically value-less Ok), while
        // BeforeWrite fires with the *input* Data carrying the actual
        // payload. Filtered by channel name so writes to "error" / "debug"
        // don't get captured.
        var outputBuf = new StringBuilder();
        var outputBinding = new EventBinding(
            app.@event.Trigger.BeforeWrite,
            async (context, _, written) =>
            {
                // Append newline per write — the stream-channel's text serializer
                // adds one on flush, so this matches "what stdout sees". A
                // payload that already ends in \n just gets a blank line, which
                // is still readable in the UI.
                var wv = written == null ? null : await written.Value();
                if (wv != null)
                    outputBuf.Append(wv).Append('\n');
                return context.Ok();
            },
            channelName: app.channel.list.@this.Output,
            priority: int.MaxValue,
            stopOnError: false);
        childApp.User.Context.Events.Register(outputBinding);

        // Per-step timing — only top-level steps of the entry goal. Nested
        // sub-goal steps roll up because the caller's AfterStep doesn't
        // fire until the sub-goal returns.
        var stepStarts = new Dictionary<int, long>();
        var entryGoalPath = test.Goal.Path?.ToString();
        bool IsEntryGoalStep(global::app.goal.steps.step.@this step)
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
            // Goal.PrPath is derived from Goal.Path — already a path.@this
            // anchored at the child App's root (same root as the parent, post
            // path-canonicalization on this branch).
            var goalCall = new GoalCall { PrPath = test.Goal.PrPath };
            var result = await childApp.RunGoalAsync(goalCall, childApp.User.Context, cts.Token);
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
            test.Stdout = outputBuf.Length > 0 ? outputBuf.ToString() : null;
        }

        parentApp.Test.Coverage.Merge(childApp.Test.Coverage);
        parentApp.Test.Add(test);
    }
}
