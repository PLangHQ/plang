using System.Text.Json;
using app.tester;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 10 — test.run action. The main loop.
/// C# handler — NOT a PLang foreach, immune to the silent-skip bug that motivated
/// this whole module. Fresh App.@this per test (file boundary = App boundary),
/// semaphore-throttled parallel execution, per-test timeout via CancellationToken,
/// AfterAction subscription for coverage, child Coverage merged into parent at end.
/// test.run never throws for child-test failures — failure is data.
/// </summary>
public class RunActionTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-run-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// Creates a .test.goal + .pr pair on disk at the temp dir. Returns a global::app.tester.test.@this
    /// ready for test.run (Status=Ready, Directory=abs, PrPath relative to Directory).
    /// </summary>
    private global::app.tester.test.@this BuildFixture(string relativePath, string goalName,
        (string module, string actionName, List<Data> parameters)[] actions)
    {
        var absFile = System.IO.Path.Combine(_tempDir, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var absDir = System.IO.Path.GetDirectoryName(absFile)!;
        System.IO.Directory.CreateDirectory(absDir);

        var goalText = new System.Text.StringBuilder();
        goalText.AppendLine(goalName);
        for (int i = 0; i < actions.Length; i++)
            goalText.AppendLine($"- action {i}");
        System.IO.File.WriteAllText(absFile, goalText.ToString());

        var goal = new Goal
        {
            Name = goalName,
            Path = "/" + relativePath,
            Steps = new GoalSteps()
        };
        for (int i = 0; i < actions.Length; i++)
        {
            var step = new Step { Index = i, Text = $"action {i}" };
            step.Actions.Add(new PrAction
            {
                Module = actions[i].module,
                ActionName = actions[i].actionName,
                Parameters = actions[i].parameters
            });
            goal.Steps.Add(step);
        }
        _ = goal.Hash; // snapshot

        var prFile = System.IO.Path.Combine(absDir, ".build",
            System.IO.Path.GetFileNameWithoutExtension(absFile).ToLowerInvariant() + ".pr");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prFile)!);
        System.IO.File.WriteAllText(prFile,
            JsonSerializer.Serialize(goal, global::app.Utils.Json.CamelCaseIndented));

        return new global::app.tester.test.@this
        {
            Goal = goal,
            Status = global::app.tester.Status.Ready
        };
    }

    private async Task<Results> RunTests(List<global::app.tester.test.@this> tests, int? parallel = null, int? timeoutSec = null)
    {
        var action = new global::app.module.test.run
        {
            Context = _app.User.Context,
            Tests = tests.ToListData<global::app.tester.test.@this>(),
            Parallel = parallel.HasValue ? new global::app.data.@this<global::app.type.number.@this>("Parallel", parallel.Value, context: _app.User.Context) : null,
            Timeout = timeoutSec.HasValue ? new global::app.data.@this<global::app.type.number.@this>("Timeout", timeoutSec.Value, context: _app.User.Context) : null
        };
        var result = await action.Run();
        return (Results)(await result.Value())!;
    }

    // Each global::app.tester.test.@this gets its own App.@this instance. Two tests cannot observe each
    // other's MemoryStack, SQLite, or provider state. Headline feature of the module.
    [Test]
    public async Task Run_FreshAppPerTest_IsolationBoundaryIsFileLevel()
    {
        // TestA: sets %shared% = 1
        var testA = BuildFixture("A.test.goal", "TestA", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data>
            {
                new("Name", new global::app.variable.@this("shared"), context: _app.User.Context),
                new("Value", 1, context: _app.User.Context)
            })
        });

        // TestB: asserts %shared% is null (should be unset if isolation works)
        var testB = BuildFixture("B.test.goal", "TestB", new (string, string, List<Data>)[]
        {
            ("assert", "isNull", new List<Data>
            {
                new("Value", "%shared%", context: _app.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { testA, testB }, parallel: 1);
        var runs = results.ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs.All(r => r.Status == global::app.tester.Status.Pass)).IsTrue();
    }

    // With Config.Parallel = (global::app.type.number.@this)2 and 4 tests, at most 2 tests run concurrently.
    // Each fixture's BeforeAction delays asynchronously for long enough that another
    // fixture can enter the same window. The observed max concurrent depth equals the
    // semaphore size — parallel=2 → max 2; parallel=1 would observe max 1.
    [Test]
    public async Task Run_ParallelExecution_RespectsSemaphoreLimit()
    {
        int currentDepth = 0;
        int maxDepth = 0;
        var depthLock = new object();

        // Filter by _tempDir — static event is shared across parallel tests.
        void Probe(global::app.@this childApp)
        {
            if (!childApp.AbsolutePath.StartsWith(_tempDir)) return;
            childApp.User.Context.Events.Register(new EventBinding(
                Trigger.BeforeAction,
                async (context, action, result) =>
                {
                    lock (depthLock)
                    {
                        currentDepth++;
                        if (currentDepth > maxDepth) maxDepth = currentDepth;
                    }
                    // Hold long enough that at most semaphore-size fixtures overlap.
                    // 100ms is plenty for the scheduler to start the next task.
                    await Task.Delay(100);
                    lock (depthLock) currentDepth--;
                    return Data.Ok();
                },
                priority: int.MaxValue,
                stopOnError: false));
        }

        global::app.module.test.run.ChildAppCreated += Probe;
        try
        {
            var tests = new List<global::app.tester.test.@this>();
            for (int i = 0; i < 4; i++)
                tests.Add(BuildFixture($"T{i}.test.goal", $"T{i}", new (string, string, List<Data>)[]
                {
                    ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", i, context: _app.User.Context) })
                }));

            var results = await RunTests(tests, parallel: 2);
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(4);
            await Assert.That(runs.All(r => r.Status == global::app.tester.Status.Pass)).IsTrue();
            // The semaphore caps concurrency at 2 — the delay forces overlap to prove it.
            // A regression to parallel=1 (serial) would drop maxDepth to 1; a regression
            // to unbounded parallelism would push it to 4.
            await Assert.That(maxDepth).IsEqualTo(2);
        }
        finally
        {
            global::app.module.test.run.ChildAppCreated -= Probe;
        }
    }

    // A test that sleeps past the configured timeout → global::app.tester.Status.Timeout.
    // Test App is disposed (CancellationToken fired; IAsyncDisposable chain
    // cancels actors, providers, channels, keep-alives).
    [Test]
    public async Task Run_TimeoutExceeded_TestMarkedTimeout()
    {
        // timeout.after wrapping a long-running action — the inner action blows the
        // outer test-level timeout. For a CPU-bound-free fixture: use timer.wait
        // or a large sleep. Simplest: construct a goal that sleeps.
        var slow = BuildFixture("Slow.test.goal", "Slow", new (string, string, List<Data>)[]
        {
            ("timer", "sleep", new List<Data>
            {
                new("Ms", 5000, context: _app.User.Context) // 5s
            })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { slow }, timeoutSec: 1);
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Timeout);
    }

    // The AfterAction subscription attached to each test's App.Events records every
    // (module, action) fired inside that test into the test's own Coverage tracker.
    [Test]
    public async Task Run_AfterActionSubscription_CapturesCoverageOnChildApp()
    {
        var test = BuildFixture("Cov.test.goal", "Cov", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) }),
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("y"), context: _app.User.Context), new("Value", 2, context: _app.User.Context) })
        });

        await RunTests(new List<global::app.tester.test.@this> { test });

        var coverage = _app.Tester.Coverage;
        await Assert.That(coverage.ModuleActions.Any(x => x.Module == "variable" && x.Action == "set")).IsTrue();
    }

    // After a test completes, its App.Tester.Coverage is merged into the parent
    // App.Tester.Coverage via Coverage.Merge. Run-wide view accumulates observations
    // from all child tests (unions module.action pairs and branch-site indices).
    [Test]
    public async Task Run_TestChildCoverage_MergedIntoParentCoverage()
    {
        var test = BuildFixture("MergeA.test.goal", "M", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("a"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) })
        });

        // Pre-populate parent's coverage with something distinct
        _app.Tester.Coverage.RecordModuleAction("output", "write");

        await RunTests(new List<global::app.tester.test.@this> { test });

        var observed = _app.Tester.Coverage.ModuleActions.ToList();
        await Assert.That(observed.Any(x => x == ("output", "write"))).IsTrue();
        await Assert.That(observed.Any(x => x == ("variable", "set"))).IsTrue();
    }

    // Child App.OsDirectory is set from context.App.OsDirectory so shared
    // os/ goals (e.g. setup helpers) resolve identically in every test.
    // Uses the ChildAppCreated hook to snapshot the child's OsDirectory.
    [Test]
    public async Task Run_OsDirectory_InheritedFromParentApp()
    {
        _app.OsDirectory = "/some/os/dir";

        string? observedChildOsDir = null;
        // Filter by _tempDir prefix — ChildAppCreated is a static event, so in a
        // parallel test run other tests' child Apps would otherwise overwrite our
        // observation with their own (empty) OsDirectory.
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observedChildOsDir = childApp.OsDirectory;
        }
        global::app.module.test.run.ChildAppCreated += Probe;
        try
        {
            var test = BuildFixture("OsDir.test.goal", "S", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) })
            });

            await RunTests(new List<global::app.tester.test.@this> { test });

            await Assert.That(observedChildOsDir).IsEqualTo("/some/os/dir");
            // Parent unchanged — the propagation is one-way (parent → child).
            await Assert.That(_app.OsDirectory).IsEqualTo("/some/os/dir");
        }
        finally
        {
            global::app.module.test.run.ChildAppCreated -= Probe;
        }
    }

    // testApp.Tester.IsEnabled = true before the test starts. Subsystems that branch
    // on test mode (in-memory DBs, stubbed identity, etc.) observe the flag.
    // Uses the ChildAppCreated hook to snapshot IsEnabled directly on the child App.
    [Test]
    public async Task Run_TestingIsEnabled_SetToTrueInChildApp()
    {
        bool? observed = null;
        // Filter by _tempDir — static event is shared across parallel tests.
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observed = childApp.Tester.IsEnabled;
        }
        global::app.module.test.run.ChildAppCreated += Probe;
        try
        {
            var test = BuildFixture("IsEn.test.goal", "E", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) })
            });

            var results = await RunTests(new List<global::app.tester.test.@this> { test });
            var run = results.Single();

            await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Pass);
            await Assert.That(observed).IsEqualTo(true);
        }
        finally
        {
            global::app.module.test.run.ChildAppCreated -= Probe;
        }
    }

    // Tests with global::app.tester.Status.Stale or Skipped are NOT executed but are included in
    // the returned global::app.tester.Run[] results with their original status. Reporter shows the
    // full surface — hiding filtered tests would hurt CI visibility.
    // Side-effect probe: count ChildAppCreated invocations — only the Ready test
    // should trigger a child App. Stale/Skipped take the early-return path.
    [Test]
    public async Task Run_OnlyReadyTests_Executed_StaleAndSkippedPreserved()
    {
        int childAppsCreated = 0;
        // Filter by _tempDir — static event is shared across parallel tests.
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                Interlocked.Increment(ref childAppsCreated);
        }
        global::app.module.test.run.ChildAppCreated += Probe;
        try
        {
            var ready = BuildFixture("Ready.test.goal", "R", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) })
            });
            var stale = new global::app.tester.test.@this {
                Goal = new Goal { Name = "Stale", Path = "/Stale.test.goal" },
                Status = global::app.tester.Status.Stale, StatusReason = "no .pr" };
            var skipped = new global::app.tester.test.@this {
                Goal = new Goal { Name = "Skip", Path = "/Skip.test.goal" },
                Status = global::app.tester.Status.Skipped, StatusReason = "excluded by tag" };

            var results = await RunTests(new List<global::app.tester.test.@this> { ready, stale, skipped });
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(3);
            await Assert.That(runs.Count(r => r.Status == global::app.tester.Status.Pass)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == global::app.tester.Status.Stale)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == global::app.tester.Status.Skipped)).IsEqualTo(1);

            // Only the one Ready test spun up a child App.
            await Assert.That(childAppsCreated).IsEqualTo(1);
        }
        finally
        {
            global::app.module.test.run.ChildAppCreated -= Probe;
        }
    }

    // A test that fails an assertion: global::app.tester.Run.Status == Fail, error on the global::app.tester.Run,
    // AssertionError.Variables populated (from Batch 5). test.run itself does not
    // throw — child failure is data, not exception. Keeps the main loop parallel-safe.
    [Test]
    public async Task Run_AssertionFailureInTest_CapturedInResult_NoPropagatedException()
    {
        // Fixture sets a variable before the assert so AssertionError.Variables can
        // demonstrate it carried through test.run's failure path (end-to-end check).
        var test = BuildFixture("Fail.test.goal", "F", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("score"), context: _app.User.Context), new("Value", 42, context: _app.User.Context) }),
            ("assert", "equals", new List<Data>
            {
                new("Expected", 1, context: _app.User.Context),
                new("Actual", 2, context: _app.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { test });
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Fail);
        await Assert.That(run.Error).IsNotNull();
        await Assert.That(run.Error is global::app.error.AssertionError).IsTrue();

        // Variables snapshot flowed from assert handler → provider → test.run's
        // failure path → global::app.tester.Run.Error. Batch 5's headline feature.
        var assertionError = (global::app.error.AssertionError)run.Error!;
        await Assert.That(assertionError.Variables).IsNotNull();
        await Assert.That(assertionError.Variables!.ContainsKey("score")).IsTrue();
        // Value roundtrips through JSON (int→long via System.Text.Json);
        // normalize to long for a type-tolerant check.
        await Assert.That(Convert.ToInt64(assertionError.Variables["score"])).IsEqualTo(42L);
    }

    // Boundary: Tests=[] → global::app.tester.Run[] is empty, no exception, no subscription
    // leakage. (independent — robustness)
    [Test]
    public async Task Run_EmptyTestList_ReturnsEmptyResults_NoError()
    {
        var results = await RunTests(new List<global::app.tester.test.@this>());
        await Assert.That(results.Count).IsEqualTo(0);
    }

    // Covers the production coverage subscriber's branchLabel / branchChain paths in
    // run.cs (the block that reads result.Properties and calls Coverage.RecordBranch*).
    // Other tests assert those Coverage methods directly, but only a fixture whose test
    // runs THROUGH test.run exercises the real wiring — a typo in the Properties keys
    // here would otherwise ship silently.
    [Test]
    public async Task Run_FixtureWithConditionIf_ProductionSubscriber_RecordsBranchLabelAndChain()
    {
        // Fixture: single condition.if (Actions.Count == 1 → simple path, publishes
        // branchIndex=0/1, branchLabel="true"/"false", branchChain=["true","false"]).
        // Operator passes through as a string — the runtime resolver constructs the
        // Operator IObject at execution time. (Real .pr files store it as string too;
        // serializing Operator directly hits a Func-not-serializable NotSupportedException.)
        var fixture = BuildFixture("Cond.test.goal", "Cond", new (string, string, List<Data>)[]
        {
            ("condition", "if", new List<Data>
            {
                new("Left", 1, context: _app.User.Context),
                new("Operator", "==", context: _app.User.Context),
                new("Right", 1, context: _app.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { fixture });
        var run = results.Single();
        await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Pass);

        // Site key format: "<goalPath>:<stepIndex>" — matches run.cs:91-95.
        // Path.ToString() returns the Relative form (canonical: leading "/").
        var site = "/Cond.test.goal:0";

        // BranchLabels populated via the production subscriber reading
        // result.Properties["branchLabel"] and calling RecordBranchLabel.
        await Assert.That(_app.Tester.Coverage.BranchLabels.ContainsKey(site)).IsTrue();
        await Assert.That(_app.Tester.Coverage.BranchLabels[site].Contains("true")).IsTrue();

        // BranchChains populated via the production subscriber reading
        // result.Properties["branchChain"] and calling RecordBranchChain.
        await Assert.That(_app.Tester.Coverage.BranchChains.ContainsKey(site)).IsTrue();
        var chain = _app.Tester.Coverage.BranchChains[site];
        await Assert.That(chain.Count).IsEqualTo(2);
        await Assert.That(chain[0]).IsEqualTo("true");
        await Assert.That(chain[1]).IsEqualTo("false");

        // Indices map gets the simple-path 0 (condition was true).
        await Assert.That(_app.Tester.Coverage.Branches[site].Contains(0)).IsTrue();
    }

    // BeforeWrite output capture: writes routed to the "output" channel land on
    // Run.Output (filtered by channel name in run.cs:149). Writes to "error" do not.
    // The filter must hold both directions — an inversion would either leak Error
    // payloads into Output or drop Output writes entirely.
    [Test]
    public async Task Run_OutputCapture_OutputChannelOnly_ErrorChannelExcluded()
    {
        // Two foundational channels need to exist on the child App's User actor
        // before the fixture runs (otherwise output.write fails with ChannelNotFound).
        // Register them at ChildAppCreated time, against MemoryStreams we don't read —
        // we only assert through Run.Output, which is fed by the production
        // BeforeWrite subscriber in test/run.cs, not by reading these streams.
        var outStream = new System.IO.MemoryStream();
        var errStream = new System.IO.MemoryStream();
        void Probe(global::app.@this childApp)
        {
            if (!childApp.AbsolutePath.StartsWith(_tempDir)) return;
            childApp.User.Channel.Register(new StreamChannel(
                global::app.channel.list.@this.Output, outStream,
                ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
            childApp.User.Channel.Register(new StreamChannel(
                global::app.channel.list.@this.Error, errStream,
                ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        }
        global::app.module.test.run.ChildAppCreated += Probe;
        try
        {
            var test = BuildFixture("OutCap.test.goal", "OutCap", new (string, string, List<Data>)[]
            {
                // No `channel` param → defaults to "output".
                ("output", "write", new List<Data> { new("Data", "hello-output", context: _app.User.Context) }),
                // Explicit channel routing to "error".
                ("output", "write", new List<Data>
                {
                    new("Data", "hello-error", context: _app.User.Context),
                    new("channel", "error", context: _app.User.Context)
                })
            });

            var results = await RunTests(new List<global::app.tester.test.@this> { test });
            var run = results.Single();

            await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Pass);
            await Assert.That(run.Output).IsNotNull();
            await Assert.That(run.Output!).Contains("hello-output");
            // The error-channel write must NOT leak into Run.Output.
            await Assert.That(run.Output!).DoesNotContain("hello-error");
        }
        finally
        {
            global::app.module.test.run.ChildAppCreated -= Probe;
        }
    }

    // Per-step Timings: only the entry goal's top-level steps. Nested sub-goal
    // steps roll up into their calling step (AfterStep on the caller doesn't
    // fire until the call returns). An entry goal with 3 steps — step 1 calls a
    // 2-step sub-goal — must produce exactly 3 Timing rows, not 5, with
    // StepIndex matching the entry-goal steps.
    [Test]
    public async Task Run_Timings_OnlyEntryGoalTopLevelSteps_NestedRollUp()
    {
        // Helper sub-goal: 2 steps. Written as a sibling .pr next to the entry's
        // .build directory so goal.call's name-based resolver (slot 3 in
        // GoalCall.GetGoalAsync — `{callerDir}/.build/{name}.pr`) finds it.
        var helperGoal = new Goal
        {
            Name = "Helper",
            Path = "/Helper.goal",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "h0", Actions = new StepActions
                {
                    new PrAction { Module = "variable", ActionName = "set",
                        Parameters = new List<Data> { new("Name", new global::app.variable.@this("h0"), context: _app.User.Context), new("Value", 0, context: _app.User.Context) } }
                }},
                new Step { Index = 1, Text = "h1", Actions = new StepActions
                {
                    new PrAction { Module = "variable", ActionName = "set",
                        Parameters = new List<Data> { new("Name", new global::app.variable.@this("h1"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) } }
                }}
            }
        };
        _ = helperGoal.Hash;

        var helperPrAbs = System.IO.Path.Combine(_tempDir, ".build", "helper.pr");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(helperPrAbs)!);
        System.IO.File.WriteAllText(helperPrAbs,
            System.Text.Json.JsonSerializer.Serialize(helperGoal,
                global::app.Utils.Json.CamelCaseIndented));

        // Entry goal: 3 top-level steps. Step 1 calls Helper (which has its own
        // 2 steps). Timings should record exactly steps 0, 1, 2 of the entry.
        var entry = BuildFixture("Tim.test.goal", "Tim", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("a"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) }),
            ("goal", "call", new List<Data>
            {
                new("GoalName", new GoalCall { Name = "Helper" }, global::app.type.@this.FromName("goal.call"), context: _app.User.Context)
            }),
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("b"), context: _app.User.Context), new("Value", 2, context: _app.User.Context) })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { entry });
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Pass);
        // Exactly 3 timings — entry-goal-only, sub-goal's 2 steps rolled up.
        await Assert.That(run.Timings.Count).IsEqualTo(3);
        var indices = run.Timings.Select(t => t.StepIndex).OrderBy(i => i).ToList();
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(1);
        await Assert.That(indices[2]).IsEqualTo(2);
        // Each step recorded a real wall-clock duration; Ms is non-negative
        // (the goal.call step at index 1 bundles the sub-goal time so it's
        // typically the largest, but we don't pin the magnitude).
        foreach (var t in run.Timings)
            await Assert.That(t.Ms >= 0.0).IsTrue();
    }

    // Covers RunSingleAsync's outer catch — a handler that throws an unexpected
    // exception must not propagate out of test.run. The global::app.tester.Run records Fail with
    // the exception message preserved; subsequent tests in the same run continue.
    [Test]
    public async Task Run_FixtureThrowsUnexpectedException_CapturedAsFail_LoopContinues()
    {
        // variable.get with a name that doesn't resolve ends up throwing inside the
        // handler when assignment blows up — but more reliable: use a fixture whose
        // .pr file has a module/action that doesn't exist, forcing the dispatch to
        // return an ActionError before the handler runs. That goes through the outer
        // catch-all path only for OTHER kinds of failures. Simpler: construct a
        // fixture whose .pr is malformed JSON so goal loading throws.
        var throwing = BuildFixture("Throw.test.goal", "T", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("x"), context: _app.User.Context), new("Value", 1, context: _app.User.Context) })
        });
        // Corrupt the .pr so deserialization throws inside RunSingleAsync.
        // .pr lives at <_tempDir>/.build/<stem>.pr (BuildFixture recipe).
        var prAbs = System.IO.Path.Combine(_tempDir, ".build", "throw.test.pr");
        System.IO.File.WriteAllText(prAbs, "{ \"name\": INVALID_JSON");

        var healthy = BuildFixture("Healthy.test.goal", "H", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.variable.@this("y"), context: _app.User.Context), new("Value", 2, context: _app.User.Context) })
        });

        var results = await RunTests(new List<global::app.tester.test.@this> { throwing, healthy });
        var runs = results.ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        // Throwing fixture captured as Fail — no exception propagated.
        var failed = runs.Single(r => r.Test.Goal.Path?.ToString() == "/Throw.test.goal");
        await Assert.That(failed.Status).IsEqualTo(global::app.tester.Status.Fail);
        await Assert.That(failed.Error).IsNotNull();
        // Healthy fixture still ran — loop stayed parallel-safe.
        var passed = runs.Single(r => r.Test.Goal.Path?.ToString() == "/Healthy.test.goal");
        await Assert.That(passed.Status).IsEqualTo(global::app.tester.Status.Pass);
    }
}
