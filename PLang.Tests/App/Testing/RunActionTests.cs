using System.Text.Json;
using global::App.Test;

namespace PLang.Tests.App.Testing;

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
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-run-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        var fs = new global::App.FileSystem.Default.PLangFileSystem(_tempDir, "");
        _app = new global::App.@this(fs);
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// Creates a .test.goal + .pr pair on disk at the temp dir. Returns a TestFile
    /// ready for test.run (Status=Ready, Directory=abs, PrPath relative to Directory).
    /// </summary>
    private TestFile BuildFixture(string relativePath, string goalName,
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
            JsonSerializer.Serialize(goal, global::App.Utils.Json.CamelCaseIndented));

        var fileName = System.IO.Path.GetFileName(absFile);
        var prFileName = System.IO.Path.ChangeExtension(fileName, ".pr").ToLowerInvariant();

        return new TestFile
        {
            Path = relativePath,
            Directory = absDir,
            PrPath = ".build/" + prFileName,
            Goal = goal,
            EntryGoalName = goalName,
            GoalHash = goal.Hash,
            Status = TestStatus.Ready
        };
    }

    private async Task<Results> RunTests(List<TestFile> tests, int? parallel = null, int? timeoutSec = null)
    {
        var action = new global::App.modules.test.run
        {
            Context = _app.User.Context,
            Tests = new global::App.Data.@this<List<TestFile>>("Tests", tests),
            Parallel = parallel.HasValue ? new global::App.Data.@this<int>("Parallel", parallel.Value) : null,
            Timeout = timeoutSec.HasValue ? new global::App.Data.@this<int>("Timeout", timeoutSec.Value) : null
        };
        var result = await action.Run();
        return (Results)result.Value!;
    }

    // Each TestFile gets its own App.@this instance. Two tests cannot observe each
    // other's MemoryStack, SQLite, or provider state. Headline feature of the module.
    [Test]
    public async Task Run_FreshAppPerTest_IsolationBoundaryIsFileLevel()
    {
        // TestA: sets %shared% = 1
        var testA = BuildFixture("A.test.goal", "TestA", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data>
            {
                new("Name", "shared"),
                new("Value", 1)
            })
        });

        // TestB: asserts %shared% is null (should be unset if isolation works)
        var testB = BuildFixture("B.test.goal", "TestB", new (string, string, List<Data>)[]
        {
            ("assert", "isNull", new List<Data>
            {
                new("Value", "%shared%")
            })
        });

        var results = await RunTests(new List<TestFile> { testA, testB }, parallel: 1);
        var runs = results.ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs.All(r => r.Status == TestStatus.Pass)).IsTrue();
    }

    // With Config.Parallel=2 and 4 tests, at most 2 tests run concurrently.
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
        void Probe(global::App.@this childApp)
        {
            if (!childApp.AbsolutePath.StartsWith(_tempDir)) return;
            childApp.User.Context.Events.Register(new EventBinding(
                EventType.BeforeAction,
                async (ctx, action, result) =>
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

        global::App.modules.test.run.ChildAppCreated += Probe;
        try
        {
            var tests = new List<TestFile>();
            for (int i = 0; i < 4; i++)
                tests.Add(BuildFixture($"T{i}.test.goal", $"T{i}", new (string, string, List<Data>)[]
                {
                    ("variable", "set", new List<Data> { new("Name", "x"), new("Value", i) })
                }));

            var results = await RunTests(tests, parallel: 2);
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(4);
            await Assert.That(runs.All(r => r.Status == TestStatus.Pass)).IsTrue();
            // The semaphore caps concurrency at 2 — the delay forces overlap to prove it.
            // A regression to parallel=1 (serial) would drop maxDepth to 1; a regression
            // to unbounded parallelism would push it to 4.
            await Assert.That(maxDepth).IsEqualTo(2);
        }
        finally
        {
            global::App.modules.test.run.ChildAppCreated -= Probe;
        }
    }

    // A test that sleeps past the configured timeout → TestStatus.Timeout.
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
                new("Ms", 5000) // 5s
            })
        });

        var results = await RunTests(new List<TestFile> { slow }, timeoutSec: 1);
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(TestStatus.Timeout);
    }

    // The AfterAction subscription attached to each test's App.Events records every
    // (module, action) fired inside that test into the test's own Coverage tracker.
    [Test]
    public async Task Run_AfterActionSubscription_CapturesCoverageOnChildApp()
    {
        var test = BuildFixture("Cov.test.goal", "Cov", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", "x"), new("Value", 1) }),
            ("variable", "set", new List<Data> { new("Name", "y"), new("Value", 2) })
        });

        await RunTests(new List<TestFile> { test });

        var coverage = _app.Testing.Coverage;
        await Assert.That(coverage.ModuleActions.Any(x => x.Module == "variable" && x.Action == "set")).IsTrue();
    }

    // After a test completes, its App.Testing.Coverage is merged into the parent
    // App.Testing.Coverage via Coverage.Merge. Run-wide view accumulates observations
    // from all child tests (unions module.action pairs and branch-site indices).
    [Test]
    public async Task Run_TestChildCoverage_MergedIntoParentCoverage()
    {
        var test = BuildFixture("MergeA.test.goal", "M", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", "a"), new("Value", 1) })
        });

        // Pre-populate parent's coverage with something distinct
        _app.Testing.Coverage.RecordModuleAction("output", "write");

        await RunTests(new List<TestFile> { test });

        var observed = _app.Testing.Coverage.ModuleActions.ToList();
        await Assert.That(observed.Any(x => x == ("output", "write"))).IsTrue();
        await Assert.That(observed.Any(x => x == ("variable", "set"))).IsTrue();
    }

    // Child App.SystemDirectory is set from context.App.SystemDirectory so shared
    // system/ goals (e.g. setup helpers) resolve identically in every test.
    // Uses the ChildAppCreated hook to snapshot the child's SystemDirectory.
    [Test]
    public async Task Run_SystemDirectory_InheritedFromParentApp()
    {
        _app.SystemDirectory = "/some/system/dir";

        string? observedChildSystemDir = null;
        // Filter by _tempDir prefix — ChildAppCreated is a static event, so in a
        // parallel test run other tests' child Apps would otherwise overwrite our
        // observation with their own (empty) SystemDirectory.
        void Probe(global::App.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observedChildSystemDir = childApp.SystemDirectory;
        }
        global::App.modules.test.run.ChildAppCreated += Probe;
        try
        {
            var test = BuildFixture("SysDir.test.goal", "S", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", "x"), new("Value", 1) })
            });

            await RunTests(new List<TestFile> { test });

            await Assert.That(observedChildSystemDir).IsEqualTo("/some/system/dir");
            // Parent unchanged — the propagation is one-way (parent → child).
            await Assert.That(_app.SystemDirectory).IsEqualTo("/some/system/dir");
        }
        finally
        {
            global::App.modules.test.run.ChildAppCreated -= Probe;
        }
    }

    // testApp.Testing.IsEnabled = true before the test starts. Subsystems that branch
    // on test mode (in-memory DBs, stubbed identity, etc.) observe the flag.
    // Uses the ChildAppCreated hook to snapshot IsEnabled directly on the child App.
    [Test]
    public async Task Run_TestingIsEnabled_SetToTrueInChildApp()
    {
        bool? observed = null;
        // Filter by _tempDir — static event is shared across parallel tests.
        void Probe(global::App.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                observed = childApp.Testing.IsEnabled;
        }
        global::App.modules.test.run.ChildAppCreated += Probe;
        try
        {
            var test = BuildFixture("IsEn.test.goal", "E", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", "x"), new("Value", 1) })
            });

            var results = await RunTests(new List<TestFile> { test });
            var run = results.Single();

            await Assert.That(run.Status).IsEqualTo(TestStatus.Pass);
            await Assert.That(observed).IsEqualTo(true);
        }
        finally
        {
            global::App.modules.test.run.ChildAppCreated -= Probe;
        }
    }

    // Tests with TestStatus.Stale or Skipped are NOT executed but are included in
    // the returned TestRun[] results with their original status. Reporter shows the
    // full surface — hiding filtered tests would hurt CI visibility.
    // Side-effect probe: count ChildAppCreated invocations — only the Ready test
    // should trigger a child App. Stale/Skipped take the early-return path.
    [Test]
    public async Task Run_OnlyReadyTests_Executed_StaleAndSkippedPreserved()
    {
        int childAppsCreated = 0;
        // Filter by _tempDir — static event is shared across parallel tests.
        void Probe(global::App.@this childApp)
        {
            if (childApp.AbsolutePath.StartsWith(_tempDir))
                Interlocked.Increment(ref childAppsCreated);
        }
        global::App.modules.test.run.ChildAppCreated += Probe;
        try
        {
            var ready = BuildFixture("Ready.test.goal", "R", new (string, string, List<Data>)[]
            {
                ("variable", "set", new List<Data> { new("Name", "x"), new("Value", 1) })
            });
            var stale = new TestFile { Path = "Stale.test.goal", Directory = _tempDir,
                PrPath = ".build/stale.test.pr", Status = TestStatus.Stale, StatusReason = "no .pr" };
            var skipped = new TestFile { Path = "Skip.test.goal", Directory = _tempDir,
                PrPath = ".build/skip.test.pr", Status = TestStatus.Skipped, StatusReason = "excluded by tag" };

            var results = await RunTests(new List<TestFile> { ready, stale, skipped });
            var runs = results.ToList();

            await Assert.That(runs.Count).IsEqualTo(3);
            await Assert.That(runs.Count(r => r.Status == TestStatus.Pass)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == TestStatus.Stale)).IsEqualTo(1);
            await Assert.That(runs.Count(r => r.Status == TestStatus.Skipped)).IsEqualTo(1);

            // Only the one Ready test spun up a child App.
            await Assert.That(childAppsCreated).IsEqualTo(1);
        }
        finally
        {
            global::App.modules.test.run.ChildAppCreated -= Probe;
        }
    }

    // A test that fails an assertion: TestRun.Status == Fail, error on the TestRun,
    // AssertionError.Variables populated (from Batch 5). test.run itself does not
    // throw — child failure is data, not exception. Keeps the main loop parallel-safe.
    [Test]
    public async Task Run_AssertionFailureInTest_CapturedInResult_NoPropagatedException()
    {
        // Fixture sets a variable before the assert so AssertionError.Variables can
        // demonstrate it carried through test.run's failure path (end-to-end check).
        var test = BuildFixture("Fail.test.goal", "F", new (string, string, List<Data>)[]
        {
            ("variable", "set", new List<Data> { new("Name", "score"), new("Value", 42) }),
            ("assert", "equals", new List<Data>
            {
                new("Expected", 1),
                new("Actual", 2)
            })
        });

        var results = await RunTests(new List<TestFile> { test });
        var run = results.Single();

        await Assert.That(run.Status).IsEqualTo(TestStatus.Fail);
        await Assert.That(run.Error).IsNotNull();
        await Assert.That(run.Error is global::App.Errors.AssertionError).IsTrue();

        // Variables snapshot flowed from assert handler → provider → test.run's
        // failure path → TestRun.Error. Batch 5's headline feature.
        var assertionError = (global::App.Errors.AssertionError)run.Error!;
        await Assert.That(assertionError.Variables).IsNotNull();
        await Assert.That(assertionError.Variables!.ContainsKey("score")).IsTrue();
        // Value roundtrips through JSON (int→long via System.Text.Json);
        // normalize to long for a type-tolerant check.
        await Assert.That(Convert.ToInt64(assertionError.Variables["score"])).IsEqualTo(42L);
    }

    // Boundary: Tests=[] → TestRun[] is empty, no exception, no subscription
    // leakage. (independent — robustness)
    [Test]
    public async Task Run_EmptyTestList_ReturnsEmptyResults_NoError()
    {
        var results = await RunTests(new List<TestFile>());
        await Assert.That(results.Count).IsEqualTo(0);
    }
}
