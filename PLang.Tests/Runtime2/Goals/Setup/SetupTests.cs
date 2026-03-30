using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Goals.Setup;

public class SetupTests
{
    private string _tempDir = null!;
    private PLang.Runtime2.Engine.@this _engine = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-setup-test-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        var fs = new PLang.SafeFileSystem.PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(fs);
    }

    [After(Test)]
    public void TearDown()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Setup_Goals_OrdersSetupFirst_ThenAlphabetical()
    {
        _engine.Goals.Add(new Goal { Name = "Zebra", IsSetup = true, Path = "/Zebra.goal" });
        _engine.Goals.Add(new Goal { Name = "Setup", IsSetup = true, Path = "/Setup.goal" });
        _engine.Goals.Add(new Goal { Name = "Alpha", IsSetup = true, Path = "/Alpha.goal" });
        _engine.Goals.Add(new Goal { Name = "NormalGoal", IsSetup = false, Path = "/NormalGoal.goal" });

        var setupGoals = _engine.Goals.Setup.Goals.ToList();

        await Assert.That(setupGoals.Count).IsEqualTo(3);
        await Assert.That(setupGoals[0].Name).IsEqualTo("Setup");
        await Assert.That(setupGoals[1].Name).IsEqualTo("Alpha");
        await Assert.That(setupGoals[2].Name).IsEqualTo("Zebra");
    }

    [Test]
    public async Task Setup_ExcludesSetupGoalsFromRegularLookup()
    {
        _engine.Goals.Add(new Goal { Name = "SetupGoal", IsSetup = true, Path = "/SetupGoal.goal" });
        _engine.Goals.Add(new Goal { Name = "NormalGoal", IsSetup = false, Path = "/NormalGoal.goal" });

        var found = _engine.Goals.Get("SetupGoal");
        var normal = _engine.Goals.Get("NormalGoal");

        await Assert.That(found).IsNull();
        await Assert.That(normal).IsNotNull();
    }

    [Test]
    public async Task IsExecuted_ReturnsFalse_ForNewStep()
    {
        var step = new Step { Text = "do something" };
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Record_ThenIsExecuted_ReturnsTrue()
    {
        var step = new Step { Text = "do something", Index = 0 };

        await _engine.Goals.Setup.Record(step, _engine);
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsExecuted_ReturnsFalse_ForNullHash()
    {
        var step = new Step { Text = "" };
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RunAsync_SkipsAlreadyExecutedSteps()
    {
        // Create a setup goal with two steps
        var step1 = new Step { Index = 0, Text = "step one",
            Actions = CreateNoOpActions() };
        var step2 = new Step { Index = 1, Text = "step two",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step1, step2 })
        };
        step1.Goal = goal;
        step2.Goal = goal;

        _engine.Goals.Add(goal);

        // Pre-record step1 with a distinctive marker value via raw DataSource.
        // Record() would overwrite with {goalPath, stepIndex, stepText, executedAt, error}.
        // If step1 is skipped, the marker survives.
        await _engine.System.SettingsStore.Set("setup", "skip_hash1", new Data("skip_hash1", "MARKER_NOT_RE_EXECUTED"));

        // Run setup — step1 should be skipped, step2 should run
        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);
        await Assert.That(result.Success).IsTrue();

        // Verify step1 was skipped: marker value should still be there (not overwritten by Record)
        var step1Data = await _engine.System.SettingsStore.Get("setup", "skip_hash1");
        await Assert.That(step1Data.Success).IsTrue();
        await Assert.That(step1Data.Value?.ToString()).IsEqualTo("MARKER_NOT_RE_EXECUTED");

        // Verify step2 was executed and recorded (has executedAt metadata, not our marker)
        var step2Data = await _engine.System.SettingsStore.Get("setup", "skip_hash2");
        await Assert.That(step2Data.Success).IsTrue();
        await Assert.That(step2Data.Value?.ToString()).IsNotEqualTo("MARKER_NOT_RE_EXECUTED");
    }

    [Test]
    public async Task RunAsync_RerunsStepWithChangedHash()
    {
        var step = new Step { Index = 0, Text = "create table",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step })
        };
        step.Goal = goal;
        _engine.Goals.Add(goal);

        // Record with original hash
        await _engine.Goals.Setup.Record(step, _engine);
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step, _engine)).IsTrue();

        // Simulate changed step (different hash) — new step object with different hash
        var changedStep = new Step { Index = 0, Text = "create table v2",
            Actions = CreateNoOpActions() };
        changedStep.Goal = goal;

        // The changed step should NOT be found as executed
        await Assert.That(await _engine.Goals.Setup.IsExecuted(changedStep, _engine)).IsFalse();
    }

    [Test]
    public async Task RunAsync_SetsAndClearsContextSetup()
    {
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this()
        };
        _engine.Goals.Add(goal);

        var context = _engine.Context;

        await Assert.That(context.Setup).IsNull();

        var result = await _engine.Goals.Setup.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Setup).IsNull(); // cleared after RunAsync
    }

    [Test]
    public async Task Clone_PreservesSetup()
    {
        var context = _engine.Context;
        context.Setup = _engine.Goals.Setup;

        var clone = context.Clone();

        await Assert.That(clone.Setup).IsEqualTo(context.Setup);
    }

    [Test]
    public async Task RunAsync_FailedStepNotRecorded()
    {
        // A step that fails (unknown module) and does NOT have IgnoreError
        var step = new Step
        {
            Index = 0, Text = "failing step",
            Actions = CreateFailingActions()
        };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step })
        };
        step.Goal = goal;
        _engine.Goals.Add(goal);

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        // Setup should fail
        await Assert.That(result.Success).IsFalse();
        // Step should NOT be recorded — it needs to re-run on next startup
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step, _engine)).IsFalse();
    }

    [Test]
    public async Task RunAsync_ToleratedErrorStepIsRecorded()
    {
        // A step that fails but has IgnoreError = true
        var step = new Step
        {
            Index = 0, Text = "tolerated failure",
            OnError = new ErrorHandler { IgnoreError = true },
            Actions = CreateFailingActions()
        };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step })
        };
        step.Goal = goal;
        _engine.Goals.Add(goal);

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        // Setup should succeed (error tolerated)
        await Assert.That(result.Success).IsTrue();
        // Step SHOULD be recorded — error was tolerated
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step, _engine)).IsTrue();
    }

    [Test]
    public async Task RunAsync_AbortsSetup_WhenRecordFails()
    {
        var step = new Step { Index = 0, Text = "good step",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step })
        };
        step.Goal = goal;
        _engine.Goals.Add(goal);

        // Force DataSource creation so the DB file exists
        _ = _engine.System.SettingsStore;

        // Corrupt the database file — Record() will fail trying to write
        var dbPath = System.IO.Path.Combine(_tempDir, ".db", "system.sqlite");
        System.IO.File.WriteAllText(dbPath, "NOT A VALID SQLITE DATABASE FILE");

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        // Setup should abort because Record couldn't write to the corrupted DB
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task RunAsync_CancellationAborts()
    {
        var step1 = new Step { Index = 0, Text = "step one",
            Actions = CreateNoOpActions() };
        var step2 = new Step { Index = 1, Text = "step two",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true, Path = "/Setup.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step1, step2 })
        };
        step1.Goal = goal;
        step2.Goal = goal;
        _engine.Goals.Add(goal);

        // Cancel after the first step completes
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context, cts.Token);

        // Setup should abort with cancellation error
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("Cancelled");
    }

    // --- Discovery tests (via RunAsync, which calls DiscoverAsync internally) ---

    [Test]
    public async Task RunAsync_OnlyLoadsSetupGoals()
    {
        // Create .pr files on disk — one setup at convention path, one non-setup
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "setup.pr"),
            """{"name":"Setup","isSetup":true,"path":"/Setup.goal","steps":[]}""");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "start.pr"),
            """{"name":"Start","isSetup":false,"path":"/Start.goal","steps":[]}""");

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        // Only the setup goal should be in the collection
        var setupGoals = _engine.Goals.Setup.Goals.ToList();
        await Assert.That(setupGoals.Count).IsEqualTo(1);
        await Assert.That(setupGoals[0].Name).IsEqualTo("Setup");
        // Non-setup goal should NOT be in the collection (not at a convention path)
        await Assert.That(_engine.Goals.Get("Start")).IsNull();
    }

    [Test]
    public async Task RunAsync_NonSetupGoalsRemainLazyLoadable()
    {
        // Create .pr files on disk — one setup at convention path, one non-setup
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "setup.pr"),
            """{"name":"Setup","isSetup":true,"path":"/Setup.goal","steps":[]}""");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "normalgoal.pr"),
            """{"name":"NormalGoal","isSetup":false,"path":"/NormalGoal.goal","steps":[]}""");

        // RunAsync discovers and runs setup goals internally
        await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        // Non-setup goal should not be in collection yet
        await Assert.That(_engine.Goals.Get("NormalGoal")).IsNull();

        // But it should be lazy-loadable via GetAsync
        var lazyLoaded = await _engine.Goals.GetAsync("NormalGoal");
        await Assert.That(lazyLoaded).IsNotNull();
        await Assert.That(lazyLoaded!.Name).IsEqualTo("NormalGoal");
    }

    [Test]
    public async Task RunAsync_HandlesEmptyDirectory()
    {
        // No .pr files at all — RunAsync discovers nothing and succeeds
        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_engine.Goals.Setup.Goals.Any()).IsFalse();
    }

    [Test]
    public async Task RunAsync_DiscoversFromSetupSubfolder()
    {
        // Setup goal in Setup/.build/setup.pr (second convention path)
        var setupBuildDir = System.IO.Path.Combine(_tempDir, "Setup", ".build");
        System.IO.Directory.CreateDirectory(setupBuildDir);

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(setupBuildDir, "setup.pr"),
            """{"name":"Setup","isSetup":true,"path":"/Setup/Setup.goal","steps":[]}""");

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var setupGoals = _engine.Goals.Setup.Goals.ToList();
        await Assert.That(setupGoals.Count).IsEqualTo(1);
        await Assert.That(setupGoals[0].Name).IsEqualTo("Setup");
    }

    [Test]
    public async Task RunAsync_IgnoresNonConventionPaths()
    {
        // Setup goal in a non-standard location — should NOT be discovered
        var customDir = System.IO.Path.Combine(_tempDir, "CustomFolder", ".build");
        System.IO.Directory.CreateDirectory(customDir);

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(customDir, "setup.pr"),
            """{"name":"CustomSetup","isSetup":true,"path":"/CustomFolder/CustomSetup.goal","steps":[]}""");

        var result = await _engine.Goals.Setup.RunAsync(_engine, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        // No setup goals discovered from non-convention path
        await Assert.That(_engine.Goals.Setup.Goals.Any()).IsFalse();
    }

    // --- IsTolerableError tests ---

    [Test]
    public async Task IsTolerableError_RecognizesTableAlreadyExists()
    {
        var error = Data.FromError(new Error("SQLite Error 1: 'table users already exists'"));
        await Assert.That(_engine.Goals.Setup.IsTolerableError(error)).IsTrue();
    }

    [Test]
    public async Task IsTolerableError_RecognizesIndexAlreadyExists()
    {
        var error = Data.FromError(new Error("index idx_users_email already exists"));
        await Assert.That(_engine.Goals.Setup.IsTolerableError(error)).IsTrue();
    }

    [Test]
    public async Task IsTolerableError_RecognizesDuplicateColumnName()
    {
        var error = Data.FromError(new Error("duplicate column name: email"));
        await Assert.That(_engine.Goals.Setup.IsTolerableError(error)).IsTrue();
    }

    [Test]
    public async Task IsTolerableError_RejectsUnrelatedError()
    {
        var error = Data.FromError(new Error("connection refused"));
        await Assert.That(_engine.Goals.Setup.IsTolerableError(error)).IsFalse();
    }

    [Test]
    public async Task IsTolerableError_ReturnsFalseForSuccess()
    {
        await Assert.That(_engine.Goals.Setup.IsTolerableError(Data.Ok())).IsFalse();
    }

    /// <summary>
    /// Creates a minimal no-op Actions collection that won't fail during step execution.
    /// Steps with empty actions succeed immediately.
    /// </summary>
    private static PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this CreateNoOpActions()
    {
        return new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this();
    }

    /// <summary>
    /// Creates an Actions collection with an unknown module that will fail at runtime.
    /// </summary>
    private static PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this CreateFailingActions()
    {
        return new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
        {
            new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
            {
                Module = "nonexistent",
                ActionName = "doesnotexist",
                Parameters = new List<Data>()
            }
        };
    }
}
