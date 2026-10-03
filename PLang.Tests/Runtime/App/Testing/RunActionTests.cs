using PLang.Tests.Shared;
using System.Text.Json;
using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 10 — test.start action. The main loop.
/// C# handler — NOT a PLang foreach, immune to the silent-skip bug that motivated
/// this whole module. Fresh App.@this per test (file boundary = App boundary),
/// semaphore-throttled parallel execution, per-test timeout via CancellationToken,
/// AfterAction subscription for coverage, child Coverage merged into parent at end.
/// test.start never throws for child-test failures — failure is data.
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
        // In-memory (Create) rooted at the temp dir — the fixtures are files on disk,
        // but the settings store needn't be. Create also spins up the test session
        // the runner accumulates into.
        _app = new global::app.@this(_tempDir).Testing();
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// Creates a .test.goal + .pr pair on disk at the temp dir. Returns a global::app.test.@this
    /// ready for test.start (Status=Ready, Directory=abs, PrPath relative to Directory).
    /// </summary>
    private async Task<global::app.test.@this> BuildFixture(string relativePath, string goalName,
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
            Path = global::app.type.item.path.@this.Resolve("/" + relativePath, _app.actor.list.User.Context),
            Step = new GoalSteps()
        };
        for (int i = 0; i < actions.Length; i++)
        {
            var step = new Step { Index = i, Text = $"action {i}" };
            step.Code.Add(new PrAction
            {
                Module = _app.actor.list.User.Context.App.Module(actions[i].module),
                Name = actions[i].actionName,
                Property = global::PLang.Tests.Shared.Make.Properties(actions[i].parameters)
            });
            goal.Step.Add(step);
        }
        _ = goal.Hash; // snapshot

        var prFile = System.IO.Path.Combine(absDir, ".build",
            System.IO.Path.GetFileNameWithoutExtension(absFile).ToLowerInvariant() + ".pr");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prFile)!);
        // Write the .pr through the PLANG serializer (goal.Output, Store view) — the same path the
        // real build takes — NOT raw STJ. The reader reads the plang wire shape ({name,type,value}
        // per param); a raw-STJ dump of the Data C# surface no longer round-trips through it.
        System.IO.File.WriteAllText(prFile, await _app.actor.list.User.Context.Pr(goal));

        // the test as a run takes it — through its own door, reaching its goal
        return await global::app.test.@this.From(goal, _app.actor.list.User.Context);
    }

    private async Task<IReadOnlyList<global::app.test.@this>> RunTests(List<global::app.test.@this> tests, int? parallel = null, int? timeoutSec = null)
    {
        // how the run runs is test's setting — this run's values for it
        var run = new Dictionary<string, object?>();
        if (parallel.HasValue) run["parallel"] = parallel.Value;
        if (timeoutSec.HasValue) run["timeout"] = $"{timeoutSec.Value}s";
        if (run.Count > 0) await _app.actor.list.User.Context.Setting.Set("app.test.setting", run).IsSuccess();
        var action = new global::app.module.test.start(_app.actor.list.User.Context) { Tests = tests.ToListData<global::app.test.@this>(_app.actor.list.User.Context) };
        var result = await action.Start();
        // run returns list<test>; materialize the executed tests (each row's value is a test).
        var list = (global::app.type.item.list.@this)(await result.Value())!;
        return list.Items(_app.actor.list.User.Context).Select(r => (global::app.test.@this)r.Peek()).ToList();
    }

    // Each global::app.test.@this gets its own App.@this instance. Two tests cannot observe each
    // other's MemoryStack, SQLite, or provider state. Headline feature of the module.
    [Test]
    public async Task Run_FreshAppPerTest_IsolationBoundaryIsFileLevel()
    {
        // TestA: sets %shared% = 1
        var testA = await BuildFixture("A.test.goal", "TestA", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data>
            {
                new("Name", new global::app.type.item.variable.@this("shared"), context: _app.actor.list.User.Context),
                new("Value", 1, context: _app.actor.list.User.Context)
            })
        });

        // TestB: asserts %shared% is null (should be unset if isolation works)
        var testB = await BuildFixture("B.test.goal", "TestB", new (string, string, List<Data>)[]
        {
            ("assert", "isNull", new List<Data>
            {
                new("Value", "%shared%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.test.@this> { testA, testB }, parallel: 1);
        var runs = results.ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        await runs.AssertAllPass();
    }

    // With test's setting parallel = 2 and 4 tests, at most 2 tests run concurrently.
    // Each fixture's BeforeAction delays asynchronously for long enough that another
    // fixture can enter the same window. The observed max concurrent depth equals the
    // semaphore size — parallel=2 → max 2; parallel=1 would observe max 1.
    [Test]
    public async Task Run_ParallelExecution_RespectsSemaphoreLimit()
    {
        int currentDepth = 0;
        int maxDepth = 0;
        var depthLock = new object();
        // Each test's action waits at a gate that opens once two are inside at the same time — so the run
        // is proven to hold two at once without a clock. A serial run never opens it (the wait times out).
        var twoInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Probe(global::app.@this childApp)
        {
            if (!childApp.AbsolutePath.StartsWith(_tempDir)) return;
            childApp.type.list["action"].Own().Bind("start", global::app.@event.When.before,
                async (_, _, context) =>
                {
                    lock (depthLock)
                    {
                        currentDepth++;
                        if (currentDepth > maxDepth) maxDepth = currentDepth;
                        if (currentDepth == 2) twoInside.TrySetResult();
                    }
                    await twoInside.Task.WaitAsync(System.TimeSpan.FromSeconds(30));
                    lock (depthLock) currentDepth--;
                    return context.Ok();
                },
                childApp.actor.list.User, global::app.@event.binding.Scope.actor);
        }

        _app.test.list.Made += Probe;
        try
        {
            var tests = new List<global::app.test.@this>();
            for (int i = 0; i < 4; i++)
                tests.Add(await BuildFixture($"T{i}.test.goal", $"T{i}", new (string, string, List<Data>)[]
                {
                    ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", i, context: _app.actor.list.User.Context) })
                }));

            var results = await RunTests(tests, parallel: 2);
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(4);
            await Assert.That(runs.All(r => r.Status == global::app.test.Status.Pass)).IsTrue();
            // The semaphore caps concurrency at 2: the gate opened with two inside, and no third was ever
            // inside with them. A serial run would never open the gate.
            await Assert.That(twoInside.Task.IsCompletedSuccessfully).IsTrue();
            await Assert.That(maxDepth).IsEqualTo(2);
        }
        finally
        {
            _app.test.list.Made -= Probe;
        }
    }

    // A test that sleeps past the configured timeout → global::app.test.Status.Timeout.
    // Test App is disposed (CancellationToken fired; IAsyncDisposable chain
    // cancels actors, providers, channels, keep-alives).
    [Test]
    public async Task Run_TimeoutExceeded_TestMarkedTimeout()
    {
        // A long-running action — it blows the
        // outer test-level timeout. For a CPU-bound-free fixture: use timer.wait
        // or a large sleep. Simplest: construct a goal that sleeps.
        var slow = await BuildFixture("Slow.test.goal", "Slow", new (string, string, List<Data>)[]
        {
            ("timer", "sleep", new List<Data>
            {
                new("Duration", "5s", context: _app.actor.list.User.Context) // 5s
            })
        });

        var results = await RunTests(new List<global::app.test.@this> { slow }, timeoutSec: 1);
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Timeout);
    }

    // The AfterAction subscription attached to each test's App.Events records every
    // (module, action) fired inside that test into the test's own Coverage tracker.
    [Test]
    public async Task Run_AfterActionSubscription_CapturesCoverageOnChildApp()
    {
        var test = await BuildFixture("Cov.test.goal", "Cov", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) }),
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("y"), context: _app.actor.list.User.Context), new("Value", 2, context: _app.actor.list.User.Context) })
        });

        await RunTests(new List<global::app.test.@this> { test });

        var coverage = _app.test.list.Report.Coverage;
        await Assert.That(coverage.ModuleActions.Any(x => x.Module == "variable" && x.Action == "set")).IsTrue();
    }

    // After a test completes, its App.test.list.Report.Coverage is merged into the parent
    // App.test.list.Report.Coverage via Coverage.Merge. Run-wide view accumulates observations
    // from all child tests (unions module.action pairs and branch-site indices).
    [Test]
    public async Task Run_TestChildCoverage_MergedIntoParentCoverage()
    {
        var test = await BuildFixture("MergeA.test.goal", "M", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("a"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) })
        });

        // Pre-populate parent's coverage with something distinct
        _app.test.list.Report.Coverage.RecordModuleAction("output", "write");

        await RunTests(new List<global::app.test.@this> { test });

        var observed = _app.test.list.Report.Coverage.ModuleActions.ToList();
        await Assert.That(observed.Any(x => x == ("output", "write"))).IsTrue();
        await Assert.That(observed.Any(x => x == ("variable", "set"))).IsTrue();
    }

    // The os root is the executable's: every app — a test's child app too — resolves shared os/ goals
    // (e.g. setup helpers) from the same folder, derived, never set or copied.
    // Uses the test list's Made hook to snapshot the child's os root.
    [Test]
    public async Task Run_TheOsRoot_IsTheExecutables()
    {
        string? observedChildOsDir = null;
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observedChildOsDir = childApp.OsAbsolutePath;
        }
        _app.test.list.Made += Probe;
        try
        {
            var test = await BuildFixture("OsDir.test.goal", "S", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) })
            });

            await RunTests(new List<global::app.test.@this> { test });

            var executables = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "os"));
            await Assert.That(observedChildOsDir).IsEqualTo(executables);
            await Assert.That(_app.OsAbsolutePath).IsEqualTo(executables);
        }
        finally
        {
            _app.test.list.Made -= Probe;
        }
    }

    // testApp.Test = new global::app.test.list.@this(testApp.actor.list.System.Context) before the test starts. Subsystems that branch
    // on test mode (in-memory DBs, stubbed identity, etc.) observe the flag.
    // Uses the test list's Made hook to snapshot IsEnabled directly on the child App.
    [Test]
    public async Task Run_TestingIsEnabled_SetToTrueInChildApp()
    {
        bool? observed = null;
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observed = childApp.test.list.Session != null;
        }
        _app.test.list.Made += Probe;
        try
        {
            var test = await BuildFixture("IsEn.test.goal", "E", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) })
            });

            var results = await RunTests(new List<global::app.test.@this> { test });
            var run = results.Single();

            await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Pass);
            await Assert.That(observed).IsEqualTo(true);
        }
        finally
        {
            _app.test.list.Made -= Probe;
        }
    }

    // Tests with global::app.test.Status.Stale or Skipped are NOT executed but are included in
    // the returned global::app.test.Run[] results with their original status. Reporter shows the
    // full surface — hiding filtered tests would hurt CI visibility.
    // Side-effect probe: count Made invocations — only the Ready test
    // should trigger a child App. Stale/Skipped take the early-return path.
    [Test]
    public async Task Run_OnlyReadyTests_Executed_StaleAndSkippedPreserved()
    {
        int childAppsCreated = 0;
        void Probe(global::app.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                Interlocked.Increment(ref childAppsCreated);
        }
        _app.test.list.Made += Probe;
        try
        {
            var ready = await BuildFixture("Ready.test.goal", "R", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) })
            });
            var stale = new global::app.test.@this() {
                Goal = new Goal { Name = "Stale", Path = global::app.type.item.path.@this.Resolve("/Stale.test.goal", _app.actor.list.User.Context) },
                Status = global::app.test.Status.Stale, StatusReason = "no .pr" };
            var skipped = new global::app.test.@this() {
                Goal = new Goal { Name = "Skip", Path = global::app.type.item.path.@this.Resolve("/Skip.test.goal", _app.actor.list.User.Context) },
                Status = global::app.test.Status.Skipped, StatusReason = "excluded by tag" };

            var results = await RunTests(new List<global::app.test.@this> { ready, stale, skipped });
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(3);
            await Assert.That(runs.Count(r => r.Status == global::app.test.Status.Pass)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == global::app.test.Status.Stale)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == global::app.test.Status.Skipped)).IsEqualTo(1);

            // Only the one Ready test spun up a child App.
            await Assert.That(childAppsCreated).IsEqualTo(1);
        }
        finally
        {
            _app.test.list.Made -= Probe;
        }
    }

    // A test that fails an assertion: global::app.test.Run.Status == Fail, error on the global::app.test.Run,
    // AssertionError.Variables populated (from Batch 5). test.start itself does not
    // throw — child failure is data, not exception. Keeps the main loop parallel-safe.
    [Test]
    public async Task Run_AssertionFailureInTest_CapturedInResult_NoPropagatedException()
    {
        // Fixture sets a variable before the assert so AssertionError.Variables can
        // demonstrate it carried through test.start's failure path (end-to-end check).
        var test = await BuildFixture("Fail.test.goal", "F", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("score"), context: _app.actor.list.User.Context), new("Value", 42, context: _app.actor.list.User.Context) }),
            ("assert", "equals", new List<Data>
            {
                new("Expected", 1, context: _app.actor.list.User.Context),
                new("Actual", 2, context: _app.actor.list.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.test.@this> { test });
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Fail);
        await Assert.That(run.Error).IsNotNull();
        await Assert.That(run.Error is global::app.error.AssertionError).IsTrue();

        // Variables snapshot flowed from assert handler → provider → test.start's
        // failure path → global::app.test.Run.Error. Batch 5's headline feature.
        var assertionError = (global::app.error.AssertionError)run.Error!;
        await Assert.That(assertionError.Variables).IsNotNull();
        await Assert.That(assertionError.Variables!.Has("score")).IsTrue();
        // Value roundtrips through JSON (int→long via System.Text.Json);
        // normalize to long for a type-tolerant check.
        await Assert.That(assertionError.Variables!.Held("score")?.ToString()).IsEqualTo("42");
    }

    // Boundary: Tests=[] → global::app.test.Run[] is empty, no exception, no subscription
    // leakage. (independent — robustness)
    [Test]
    public async Task Run_EmptyTestList_ReturnsEmptyResults_NoError()
    {
        var results = await RunTests(new List<global::app.test.@this>());
        await Assert.That(results.Count).IsEqualTo(0);
    }

    // Covers the production coverage subscriber's branchLabel / branchChain paths in
    // start.cs (the block that reads result.Properties and calls Coverage.RecordBranch*).
    // Other tests assert those Coverage methods directly, but only a fixture whose test
    // runs THROUGH test.start exercises the real wiring — a typo in the Properties keys
    // here would otherwise ship silently.
    [Test]
    public async Task Run_FixtureWithConditionIf_ProductionSubscriber_RecordsBranchLabelAndChain()
    {
        // Fixture: single condition.if (Actions.Count == 1 → simple path, publishes
        // branchIndex=0/1, branchLabel="true"/"false", branchChain=["true","false"]).
        // Operator passes through as a string — the runtime resolver constructs the
        // Operator IObject at execution time. (Real .pr files store it as string too;
        // serializing Operator directly hits a Func-not-serializable NotSupportedException.)
        var fixture = await BuildFixture("Cond.test.goal", "Cond", new (string, string, List<Data>)[]
        {
            ("condition", "if", new List<Data>
            {
                new("Left", 1, context: _app.actor.list.User.Context),
                new("Operator", "==", context: _app.actor.list.User.Context),
                new("Right", 1, context: _app.actor.list.User.Context)
            })
        });

        var results = await RunTests(new List<global::app.test.@this> { fixture });
        var run = results.Single();
        await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Pass);

        // Site key format: "<goalPath>:<stepIndex>" — matches start.cs:91-95.
        // Path.ToString() returns the Relative form (canonical: leading "/").
        var site = "/Cond.test.goal:0";

        // BranchLabels populated via the production subscriber reading
        // (await result.Properties.Value("branchLabel")) and calling RecordBranchLabel.
        await Assert.That(_app.test.list.Report.Coverage.BranchLabels.ContainsKey(site)).IsTrue();
        await Assert.That(_app.test.list.Report.Coverage.BranchLabels[site].Contains("true")).IsTrue();

        // BranchChains populated via the production subscriber reading
        // (await result.Properties.Value("branchChain")) and calling RecordBranchChain.
        await Assert.That(_app.test.list.Report.Coverage.BranchChains.ContainsKey(site)).IsTrue();
        var chain = _app.test.list.Report.Coverage.BranchChains[site];
        await Assert.That(chain.Count).IsEqualTo(2);
        await Assert.That(chain[0]).IsEqualTo("true");
        await Assert.That(chain[1]).IsEqualTo("false");

        // Indices map gets the simple-path 0 (condition was true).
        await Assert.That(_app.test.list.Report.Coverage.Branches[site].Contains(0)).IsTrue();
    }

    // BeforeWrite output capture: writes routed to the "output" channel land on
    // Run.Output (filtered by channel name in start.cs:149). Writes to "error" do not.
    // The filter must hold both directions — an inversion would either leak Error
    // payloads into Output or drop Output writes entirely.
    [Test]
    public async Task Run_OutputCapture_OutputChannelOnly_ErrorChannelExcluded()
    {
        // The child App's output is the test's session (test.start opens it); the error channel
        // is a MemoryStream we don't read — we only assert through the test's Stdout, which is the
        // session's text.
        var errStream = new System.IO.MemoryStream();
        void Probe(global::app.@this childApp)
        {
            if (!childApp.AbsolutePath.StartsWith(_tempDir)) return;
            childApp.actor.list.User.Channel.Register(new StreamChannel(
                global::app.channel.list.@this.Error, errStream,
                ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        }
        _app.test.list.Made += Probe;
        try
        {
            var test = await BuildFixture("OutCap.test.goal", "OutCap", new (string, string, List<Data>)[]
            {
                // No `channel` param → defaults to "output".
                ("output", "write", new List<Data> { new("Data", "hello-output", context: _app.actor.list.User.Context) }),
                // Explicit channel routing to "error".
                ("output", "write", new List<Data>
                {
                    new("Data", "hello-error", context: _app.actor.list.User.Context),
                    new("channel", "error", context: _app.actor.list.User.Context)
                })
            });

            var results = await RunTests(new List<global::app.test.@this> { test });
            var run = results.Single();

            await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Pass);
            await Assert.That(run.Stdout).IsNotNull();
            await Assert.That(run.Stdout!.Clr<string>()!).Contains("hello-output");
            // The error-channel write must NOT leak into Run.Output.
            await Assert.That(run.Stdout!.Clr<string>()!).DoesNotContain("hello-error");
        }
        finally
        {
            _app.test.list.Made -= Probe;
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
            Path = global::app.type.item.path.@this.Resolve("/Helper.goal", _app.actor.list.User.Context),
            Step = new GoalSteps
            {
                new Step { Index = 0, Text = "h0", Code = new StepActions
                {
                    new PrAction { Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
                        Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new("Name", new global::app.type.item.variable.@this("h0"), context: _app.actor.list.User.Context), new("Value", 0, context: _app.actor.list.User.Context) }) }
                }},
                new Step { Index = 1, Text = "h1", Code = new StepActions
                {
                    new PrAction { Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
                        Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new("Name", new global::app.type.item.variable.@this("h1"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) }) }
                }}
            }
        };
        _ = helperGoal.Hash;

        var helperPrAbs = System.IO.Path.Combine(_tempDir, ".build", "helper.pr");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(helperPrAbs)!);
        // Write via the plang serializer (goal.Output, Store) — the real build path — not raw STJ,
        // so the reader can read it back (see BuildFixture).
        System.IO.File.WriteAllText(helperPrAbs, await _app.actor.list.User.Context.Pr(helperGoal));
        System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "Helper.goal"), "Helper\n");

        // Entry goal: 3 top-level steps. Step 1 calls Helper (which has its own
        // 2 steps). Timings should record exactly steps 0, 1, 2 of the entry.
        var entry = await BuildFixture("Tim.test.goal", "Tim", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("a"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) }),
            ("goal", "call", new List<Data>
            {
                new("Name", "Helper", context: _app.actor.list.User.Context)
            }),
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("b"), context: _app.actor.list.User.Context), new("Value", 2, context: _app.actor.list.User.Context) })
        });

        var results = await RunTests(new List<global::app.test.@this> { entry });
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(global::app.test.Status.Pass);
        // Exactly 3 timings — entry-goal-only, sub-goal's 2 steps rolled up.
        await Assert.That(run.Timings.Count).IsEqualTo(3);
        var indices = run.Timings.Items(_app.actor.list.User.Context).Select(t => ((global::app.test.timing.@this)t.Peek()).Step.Index).OrderBy(i => i).ToList();
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(1);
        await Assert.That(indices[2]).IsEqualTo(2);
        // Each step recorded a real wall-clock duration; Ms is non-negative
        // (the goal.call step at index 1 bundles the sub-goal time so it's
        // typically the largest, but we don't pin the magnitude).
        foreach (var t in (run.Timings).Items(_app.actor.list.User.Context))
            await Assert.That(((global::app.test.timing.@this)t.Peek()).Elapsed.Value.TotalMilliseconds >= 0.0).IsTrue();
    }

    // Covers RunSingleAsync's outer catch — a handler that throws an unexpected
    // exception must not propagate out of test.start. The global::app.test.Run records Fail with
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
        var throwing = await BuildFixture("Throw.test.goal", "T", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("x"), context: _app.actor.list.User.Context), new("Value", 1, context: _app.actor.list.User.Context) })
        });
        // Corrupt the .pr so deserialization throws inside RunSingleAsync.
        // .pr lives at <_tempDir>/.build/<stem>.pr (BuildFixture recipe).
        var prAbs = System.IO.Path.Combine(_tempDir, ".build", "throw.test.pr");
        System.IO.File.WriteAllText(prAbs, "{ \"name\": INVALID_JSON");

        var healthy = await BuildFixture("Healthy.test.goal", "H", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", new global::app.type.item.variable.@this("y"), context: _app.actor.list.User.Context), new("Value", 2, context: _app.actor.list.User.Context) })
        });

        var results = await RunTests(new List<global::app.test.@this> { throwing, healthy });
        var runs = results.ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        // Throwing fixture captured as Fail — no exception propagated.
        var failed = runs.Single(r => r.Goal.Path?.ToString() == "/Throw.test.goal");
        await Assert.That(failed.Status).IsEqualTo(global::app.test.Status.Fail);
        await Assert.That(failed.Error).IsNotNull();
        // Healthy fixture still ran — loop stayed parallel-safe.
        var passed = runs.Single(r => r.Goal.Path?.ToString() == "/Healthy.test.goal");
        await Assert.That(passed.Status).IsEqualTo(global::app.test.Status.Pass);
    }
}
