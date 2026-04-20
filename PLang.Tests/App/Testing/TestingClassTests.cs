using global::App.Test;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 1 — Testing class shape and configuration.
/// The Testing class owns runner state (IsEnabled, per-test slot) AND runner configuration
/// (timeout, parallel, include, exclude, verbose) — no separate Config class.
/// Collaborator classes Results and Coverage are owned by Testing but tested separately
/// (ResultsTests.cs, CoverageTests.cs in Batch 2).
/// </summary>
public class TestingClassTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // Fresh Testing starts disabled — today's stub behavior is preserved.
    [Test]
    public async Task NewInstance_IsEnabled_FalseByDefault()
    {
        await Assert.That(_app.Testing.IsEnabled).IsFalse();
    }

    // Testing owns a Results collection, initialized, empty on construction.
    [Test]
    public async Task NewInstance_Results_InitializedEmpty()
    {
        await Assert.That(_app.Testing.Results).IsNotNull();
        await Assert.That(_app.Testing.Results.Count).IsEqualTo(0);
    }

    // Testing owns a Coverage tracker, initialized with zero observed module.action and branch entries.
    [Test]
    public async Task NewInstance_Coverage_InitializedEmpty()
    {
        await Assert.That(_app.Testing.Coverage).IsNotNull();
        await Assert.That(_app.Testing.Coverage.ModuleActions.Any()).IsFalse();
        await Assert.That(_app.Testing.Coverage.Branches.Count).IsEqualTo(0);
    }

    // Per-test in-flight state slot starts null; test.run assigns it for the currently running test.
    [Test]
    public async Task NewInstance_CurrentTest_NullUntilAssigned()
    {
        await Assert.That(_app.Testing.CurrentTest).IsNull();
    }

    // Architect spec: TimeoutSeconds defaults to 30.
    [Test]
    public async Task NewInstance_TimeoutSeconds_DefaultIs30()
    {
        await Assert.That(_app.Testing.TimeoutSeconds).IsEqualTo(30);
    }

    // Architect spec: Parallel defaults to Environment.ProcessorCount.
    [Test]
    public async Task NewInstance_Parallel_DefaultIsProcessorCount()
    {
        await Assert.That(_app.Testing.Parallel).IsEqualTo(Environment.ProcessorCount);
    }

    // No tag filter by default — Include is empty, meaning every discovered test matches.
    [Test]
    public async Task NewInstance_Include_DefaultIsEmpty()
    {
        await Assert.That(_app.Testing.Include.Count).IsEqualTo(0);
    }

    // No tag filter by default — Exclude is empty, meaning nothing is excluded.
    [Test]
    public async Task NewInstance_Exclude_DefaultIsEmpty()
    {
        await Assert.That(_app.Testing.Exclude.Count).IsEqualTo(0);
    }

    // Quiet mode by default — output.write is captured and shown only on failure.
    [Test]
    public async Task NewInstance_Verbose_DefaultIsFalse()
    {
        await Assert.That(_app.Testing.Verbose).IsFalse();
    }

    // --test={"timeout":60,"parallel":4,"include":["fast"],"exclude":["slow"],"verbose":true}
    // applies all five fields to the Testing instance.
    [Test]
    public async Task Configure_FromJson_AllFieldsApplied()
    {
        var config = new Dictionary<string, object?>
        {
            ["timeout"] = 60,
            ["parallel"] = 4,
            ["include"] = new List<object?> { "fast" },
            ["exclude"] = new List<object?> { "slow" },
            ["verbose"] = true
        };

        var result = _app.Testing.Apply(config);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Testing.TimeoutSeconds).IsEqualTo(60);
        await Assert.That(_app.Testing.Parallel).IsEqualTo(4);
        await Assert.That(_app.Testing.Include.Contains("fast")).IsTrue();
        await Assert.That(_app.Testing.Exclude.Contains("slow")).IsTrue();
        await Assert.That(_app.Testing.Verbose).IsTrue();
    }
}
