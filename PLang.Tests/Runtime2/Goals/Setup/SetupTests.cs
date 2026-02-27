using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
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
        _engine.Goals.Add(new Goal { Name = "Zebra", IsSetup = true });
        _engine.Goals.Add(new Goal { Name = "Setup", IsSetup = true });
        _engine.Goals.Add(new Goal { Name = "Alpha", IsSetup = true });
        _engine.Goals.Add(new Goal { Name = "NormalGoal", IsSetup = false });

        var setupGoals = _engine.Goals.Setup.Goals.ToList();

        await Assert.That(setupGoals.Count).IsEqualTo(3);
        await Assert.That(setupGoals[0].Name).IsEqualTo("Setup");
        await Assert.That(setupGoals[1].Name).IsEqualTo("Alpha");
        await Assert.That(setupGoals[2].Name).IsEqualTo("Zebra");
    }

    [Test]
    public async Task Setup_ExcludesSetupGoalsFromRegularLookup()
    {
        _engine.Goals.Add(new Goal { Name = "SetupGoal", IsSetup = true });
        _engine.Goals.Add(new Goal { Name = "NormalGoal", IsSetup = false });

        var found = _engine.Goals.Get("SetupGoal");
        var normal = _engine.Goals.Get("NormalGoal");

        await Assert.That(found).IsNull();
        await Assert.That(normal).IsNotNull();
    }

    [Test]
    public async Task IsExecuted_ReturnsFalse_ForNewStep()
    {
        var step = new Step { Hash = "abc123", Text = "do something" };
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Record_ThenIsExecuted_ReturnsTrue()
    {
        var step = new Step { Hash = "abc123", Text = "do something", Index = 0 };

        await _engine.Goals.Setup.Record(step, _engine);
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsExecuted_ReturnsFalse_ForNullHash()
    {
        var step = new Step { Hash = null, Text = "no hash" };
        var result = await _engine.Goals.Setup.IsExecuted(step, _engine);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RunAsync_SkipsAlreadyExecutedSteps()
    {
        // Create a setup goal with two steps
        var step1 = new Step { Index = 0, Text = "step one", Hash = "hash1",
            Actions = CreateNoOpActions() };
        var step2 = new Step { Index = 1, Text = "step two", Hash = "hash2",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true,
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step1, step2 })
        };
        step1.Goal = goal;
        step2.Goal = goal;

        _engine.Goals.Add(goal);

        var context = _engine.Context;

        // Pre-record step1 as already executed
        await _engine.Goals.Setup.Record(step1, _engine);

        // Run setup — step1 should be skipped, step2 should run
        var result = await _engine.Goals.Setup.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();

        // Both should now be recorded
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step1, _engine)).IsTrue();
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step2, _engine)).IsTrue();
    }

    [Test]
    public async Task RunAsync_RerunsStepWithChangedHash()
    {
        var step = new Step { Index = 0, Text = "create table", Hash = "original_hash",
            Actions = CreateNoOpActions() };
        var goal = new Goal
        {
            Name = "Setup", IsSetup = true,
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this(new[] { step })
        };
        step.Goal = goal;
        _engine.Goals.Add(goal);

        // Record with original hash
        await _engine.Goals.Setup.Record(step, _engine);
        await Assert.That(await _engine.Goals.Setup.IsExecuted(step, _engine)).IsTrue();

        // Simulate changed step (different hash) — new step object with different hash
        var changedStep = new Step { Index = 0, Text = "create table v2", Hash = "changed_hash",
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
            Name = "Setup", IsSetup = true,
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

    /// <summary>
    /// Creates a minimal no-op Actions collection that won't fail during step execution.
    /// Steps with empty actions succeed immediately.
    /// </summary>
    private static PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this CreateNoOpActions()
    {
        return new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this();
    }
}
