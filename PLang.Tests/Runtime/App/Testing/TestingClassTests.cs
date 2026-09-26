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
        await Assert.That(_app.Test != null).IsFalse();
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

    // --test={"timeoutSeconds":60,"parallel":4,"include":["fast"],"exclude":["slow"],"verbose":true}
    // applies all five fields to the Testing instance via the setting walk (keys are property names).
    [Test]
    public async Task Configure_FromJson_AllFieldsApplied()
    {
        var config = new Dictionary<string, object?>
        {
            ["timeoutSeconds"] = 60,
            ["parallel"] = 4,
            ["include"] = new List<object?> { "fast" },
            ["exclude"] = new List<object?> { "slow" },
            ["verbose"] = true
        };

        var result = _app.Setting.Set(_app.Test, config);

        await result.IsSuccess();
        await Assert.That(_app.Test.TimeoutSeconds.ToInt32()).IsEqualTo(60);
        await Assert.That(_app.Test.Parallel.ToInt32()).IsEqualTo(4);
        await _app.Test.Include.Contains("fast", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.Test.Exclude.Contains("slow", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.Test.Verbose.IsTrue();
    }

    // A choice setting given as CLI text (--test={"format":"junit"}) is made by the choice itself.
    [Test]
    public async Task Configure_AChoiceFromItsText()
    {
        var result = _app.Setting.Set(_app.Test, new Dictionary<string, object?> { ["format"] = "junit" });

        await result.IsSuccess();
        await Assert.That(_app.Test.Format.Clr<global::app.test.Format>()).IsEqualTo(global::app.test.Format.JUnit);
    }

    // Include/Exclude are replace-semantics — the walk sets a fresh list<text>, so a second
    // --test= call wipes the prior contents rather than accumulating tags.
    [Test]
    public async Task Configure_FromJson_IncludeAndExclude_ReplaceExisting()
    {
        _app.Test.Include.Add(new global::app.type.item.text.@this("oldInclude"));
        _app.Test.Exclude.Add(new global::app.type.item.text.@this("oldExclude"));

        var result = _app.Setting.Set(_app.Test, new Dictionary<string, object?>
        {
            ["include"] = new List<object?> { "newInclude" },
            ["exclude"] = new List<object?> { "newExclude" }
        });

        await result.IsSuccess();
        await Assert.That(_app.Test.Include.Count.ToInt32()).IsEqualTo(1);
        await _app.Test.Include.Contains("newInclude", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.Test.Include.Contains("oldInclude", global::PLang.Tests.TestApp.SharedContext).IsFalse();
        await Assert.That(_app.Test.Exclude.Count.ToInt32()).IsEqualTo(1);
        await _app.Test.Exclude.Contains("newExclude", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.Test.Exclude.Contains("oldExclude", global::PLang.Tests.TestApp.SharedContext).IsFalse();
    }

    // Unknown config keys are rejected — the setting walk is strict (same as --app/--build/
    // --callstack): a key with no public-settable property surfaces an UnknownSetting error.
    [Test]
    public async Task Configure_FromJson_UnknownKey_Rejected()
    {
        var result = _app.Setting.Set(_app.Test, new Dictionary<string, object?>
        {
            ["timeoutSeconds"] = 10,
            ["futureOption"] = "not a valid key yet"
        });

        await result.IsFailure();
    }

    private static global::app.goal.@this TaggedGoal(string tag)
    {
        var goal = new global::app.goal.@this
        {
            Name = "T",
            Path = global::app.type.item.path.@this.Resolve("/Tests/T.test.goal", global::PLang.Tests.TestApp.SharedContext)
        };
        goal.Tag.Add(new global::app.type.item.tag.@this(tag));
        return goal;
    }

    // The run's tag filter is the session's own: a test it leaves out is born Skipped, with the reason.
    [Test]
    public async Task Create_ExcludedTest_ComesBackSkippedWithItsReason()
    {
        _app.Test.Exclude.Add(new global::app.type.item.text.@this("slow"));

        var test = await _app.Test.Create(TaggedGoal("slow"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
        await Assert.That(test.StatusReason?.ToString()).IsEqualTo("excluded by tag");
    }

    // Exclude set from the CLI shape (--test={"exclude":["slow"]}) binds and filters.
    [Test]
    public async Task Create_ExcludeSetThroughTheWalk_Filters()
    {
        var set = _app.Setting.Set(_app.Test, new Dictionary<string, object?> { ["exclude"] = new List<object?> { "slow" } });
        await set.IsSuccess();

        var test = await _app.Test.Create(TaggedGoal("slow"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
        await Assert.That(test.StatusReason?.ToString()).IsEqualTo("excluded by tag");
    }

    [Test]
    public async Task Create_TakenTest_IsReady()
    {
        _app.Test.Exclude.Add(new global::app.type.item.text.@this("slow"));

        var test = await _app.Test.Create(TaggedGoal("fast"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Ready);
        await Assert.That(test.StatusReason).IsNull();
    }
}
