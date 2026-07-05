using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 1 — Testing class shape and configuration.
/// The Testing class owns runner state (IsEnabled, per-test slot) AND runner configuration
/// (timeout, parallel, include, exclude, verbose) — no separate Config class.
/// Collaborator classes Results and Coverage are owned by Testing but tested separately
/// (ResultsTests.cs, CoverageTests.cs in Batch 2).
/// </summary>
public class TestingClassTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    // Fresh Testing starts disabled — today's stub behavior is preserved.
    [Test]
    public async Task NewInstance_IsEnabled_FalseByDefault()
    {
        await Assert.That(_app.Test.IsEnabled).IsFalse();
    }

    // Testing owns a Results collection, initialized, empty on construction.
    [Test]
    public async Task NewInstance_Results_InitializedEmpty()
    {
        await Assert.That(_app.Test).IsNotNull();
        await Assert.That(_app.Test.Count).IsEqualTo(0);
    }

    // Testing owns a Coverage tracker, initialized with zero observed module.action and branch entries.
    [Test]
    public async Task NewInstance_Coverage_InitializedEmpty()
    {
        await Assert.That(_app.Test.Coverage).IsNotNull();
        await Assert.That(_app.Test.Coverage.ModuleActions.Any()).IsFalse();
        await Assert.That(_app.Test.Coverage.Branches.Count).IsEqualTo(0);
    }

    // Per-test in-flight state slot starts null; test.run assigns it for the currently running test.
    [Test]
    public async Task NewInstance_CurrentTest_NullUntilAssigned()
    {
        await Assert.That(_app.Test.Current).IsNull();
    }

    // Architect spec: TimeoutSeconds defaults to 30.
    [Test]
    public async Task NewInstance_TimeoutSeconds_DefaultIs30()
    {
        await Assert.That(_app.Test.TimeoutSeconds.ToInt32()).IsEqualTo(30);
    }

    // Architect spec: Parallel defaults to Environment.ProcessorCount.
    [Test]
    public async Task NewInstance_Parallel_DefaultIsProcessorCount()
    {
        await Assert.That(_app.Test.Parallel.ToInt32()).IsEqualTo(Environment.ProcessorCount);
    }

    // No tag filter by default — Include is empty, meaning every discovered test matches.
    [Test]
    public async Task NewInstance_Include_DefaultIsEmpty()
    {
        await Assert.That(_app.Test.Include.Count.ToInt32()).IsEqualTo(0);
    }

    // No tag filter by default — Exclude is empty, meaning nothing is excluded.
    [Test]
    public async Task NewInstance_Exclude_DefaultIsEmpty()
    {
        await Assert.That(_app.Test.Exclude.Count.ToInt32()).IsEqualTo(0);
    }

    // Quiet mode by default — output.write is captured and shown only on failure.
    [Test]
    public async Task NewInstance_Verbose_DefaultIsFalse()
    {
        await _app.Test.Verbose.IsFalse();
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

        var result = _app.Test.Apply(config);

        await result.IsSuccess();
        await Assert.That(_app.Test.TimeoutSeconds.ToInt32()).IsEqualTo(60);
        await Assert.That(_app.Test.Parallel.ToInt32()).IsEqualTo(4);
        await _app.Test.Include.Contains("fast").IsTrue();
        await _app.Test.Exclude.Contains("slow").IsTrue();
        await _app.Test.Verbose.IsTrue();
    }

    // Applying Include/Exclude is replace-semantics, not merge — a second Apply wipes
    // the prior contents so repeated --test= calls don't silently accumulate tags.
    [Test]
    public async Task Configure_FromJson_IncludeAndExclude_ReplaceExisting()
    {
        _app.Test.Include.Add(new global::app.type.text.@this("oldInclude"));
        _app.Test.Exclude.Add(new global::app.type.text.@this("oldExclude"));

        var result = _app.Test.Apply(new Dictionary<string, object?>
        {
            ["include"] = new List<object?> { "newInclude" },
            ["exclude"] = new List<object?> { "newExclude" }
        });

        await result.IsSuccess();
        await Assert.That(_app.Test.Include.Count.ToInt32()).IsEqualTo(1);
        await _app.Test.Include.Contains("newInclude").IsTrue();
        await _app.Test.Include.Contains("oldInclude").IsFalse();
        await Assert.That(_app.Test.Exclude.Count.ToInt32()).IsEqualTo(1);
        await _app.Test.Exclude.Contains("newExclude").IsTrue();
        await _app.Test.Exclude.Contains("oldExclude").IsFalse();
    }

    // Unknown config keys are silently ignored — forward-compat contract so users can
    // carry config files written for newer versions without the runner erroring on them.
    [Test]
    public async Task Configure_FromJson_UnknownKey_IgnoredReturnsOk()
    {
        var result = _app.Test.Apply(new Dictionary<string, object?>
        {
            ["timeout"] = 10,
            ["futureOption"] = "not a valid key yet"
        });

        await result.IsSuccess();
        await Assert.That(_app.Test.TimeoutSeconds.ToInt32()).IsEqualTo(10);
    }
}
