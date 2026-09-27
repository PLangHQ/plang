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

    // A plain App is not testing: no session is open until a run opens one.
    [Test]
    public async Task NewInstance_IsEnabled_FalseByDefault()
    {
        await using var plain = new global::app.@this("/test");
        await Assert.That(plain.test.list.Session == null).IsTrue();
        await Assert.That(plain.Mode.Value).IsEqualTo(global::app.Mode.Run);
    }

    // Opening a run's session makes the App testing; closing it ends that.
    [Test]
    public async Task OpenAndClose_TheSession_IsTheTestingState()
    {
        await using var plain = new global::app.@this("/test");
        plain.test.list.Open();
        await Assert.That(plain.Mode.Value).IsEqualTo(global::app.Mode.Test);
        await plain.test.list.Close();
        await Assert.That(plain.Mode.Value).IsEqualTo(global::app.Mode.Run);
    }

    // The test list always exists, empty on construction.
    [Test]
    public async Task NewInstance_Results_InitializedEmpty()
    {
        await Assert.That(_app.test.list.Items().Count()).IsEqualTo(0);
    }

    // Testing owns a Coverage tracker, initialized with zero observed module.action and branch entries.
    [Test]
    public async Task NewInstance_Coverage_InitializedEmpty()
    {
        await Assert.That(_app.test.list.Report.Coverage).IsNotNull();
        await Assert.That(_app.test.list.Report.Coverage.ModuleActions.Any()).IsFalse();
        await Assert.That(_app.test.list.Report.Coverage.Branches.Count).IsEqualTo(0);
    }

    // The run's own session holds no test: no test is current.
    [Test]
    public async Task NewInstance_CurrentTest_NullOutsideATest()
    {
        await Assert.That(global::app.test.@this.Current(_app.User.Context)).IsNull();
    }

    // A test's session makes its test the current one for the actor it is on, and takes its writes.
    [Test]
    public async Task TestSession_IsCurrent_AndTakesTheWrites()
    {
        await using var plain = new global::app.@this("/test");
        var test = new global::app.test.@this { Goal = new global::app.goal.@this { Name = "Start" } };
        var session = plain.test.list.Open(test);

        await Assert.That(global::app.test.@this.Current(plain.User.Context)).IsSameReferenceAs(test);
        await plain.User.Channel[global::app.channel.list.@this.Output].WriteText("hello");
        await Assert.That(session.Text?.ToString()).IsEqualTo("hello\n");
    }

    // Architect spec: TimeoutSeconds defaults to 30.
    [Test]
    public async Task NewInstance_TimeoutSeconds_DefaultIs30()
    {
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().TimeoutSeconds.ToInt32()).IsEqualTo(30);
    }

    // Parallel defaults to 0 — one per processor (the run reads <= 0 so) — the same value on every machine.
    [Test]
    public async Task NewInstance_Parallel_DefaultIsOnePerProcessor()
    {
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Parallel.ToInt32()).IsEqualTo(0);
    }

    // No tag filter by default — Include is empty, meaning every discovered test matches.
    [Test]
    public async Task NewInstance_Include_DefaultIsEmpty()
    {
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Include.Count.ToInt32()).IsEqualTo(0);
    }

    // No tag filter by default — Exclude is empty, meaning nothing is excluded.
    [Test]
    public async Task NewInstance_Exclude_DefaultIsEmpty()
    {
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Exclude.Count.ToInt32()).IsEqualTo(0);
    }

    // --test={"timeoutSeconds":60,"parallel":4,"include":["fast"],"exclude":["slow"]}
    // applies each field to the run's setting via the setting walk (keys are property names).
    [Test]
    public async Task Configure_FromJson_AllFieldsApplied()
    {
        var config = new Dictionary<string, object?>
        {
            ["timeoutSeconds"] = 60,
            ["parallel"] = 4,
            ["include"] = new List<object?> { "fast" },
            ["exclude"] = new List<object?> { "slow" },
        };

        var result = _app.System.Setting.Set("app.test.setting", config);

        await result.IsSuccess();
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().TimeoutSeconds.ToInt32()).IsEqualTo(60);
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Parallel.ToInt32()).IsEqualTo(4);
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Include.Contains("fast", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Exclude.Contains("slow", global::PLang.Tests.TestApp.SharedContext).IsTrue();
    }

    // A choice setting given as CLI text (--test={"format":"junit"}) is made by the choice itself.
    [Test]
    public async Task Configure_AChoiceFromItsText()
    {
        var result = _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["format"] = "junit" });

        await result.IsSuccess();
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Format.Clr<global::app.test.Format>()).IsEqualTo(global::app.test.Format.JUnit);
    }

    // Include/Exclude are replace-semantics — the walk sets a fresh list<text>, so a second
    // --test= call wipes the prior contents rather than accumulating tags.
    [Test]
    public async Task Configure_FromJson_IncludeAndExclude_ReplaceExisting()
    {
        _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["include"] = new List<object?> { "oldInclude" } });
        _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["exclude"] = new List<object?> { "oldExclude" } });

        var result = _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?>
        {
            ["include"] = new List<object?> { "newInclude" },
            ["exclude"] = new List<object?> { "newExclude" }
        });

        await result.IsSuccess();
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Include.Count.ToInt32()).IsEqualTo(1);
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Include.Contains("newInclude", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Include.Contains("oldInclude", global::PLang.Tests.TestApp.SharedContext).IsFalse();
        await Assert.That(_app.System.Context.Setting.Of<global::app.test.setting.@this>().Exclude.Count.ToInt32()).IsEqualTo(1);
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Exclude.Contains("newExclude", global::PLang.Tests.TestApp.SharedContext).IsTrue();
        await _app.System.Context.Setting.Of<global::app.test.setting.@this>().Exclude.Contains("oldExclude", global::PLang.Tests.TestApp.SharedContext).IsFalse();
    }

    // Unknown config keys are rejected — the setting walk is strict (same as --app/--build/
    // --callstack): a key with no public-settable property surfaces an UnknownSetting error.
    [Test]
    public async Task Configure_FromJson_UnknownKey_Rejected()
    {
        var result = _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?>
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
    public async Task From_ExcludedTest_ComesBackSkippedWithItsReason()
    {
        _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["exclude"] = new List<object?> { "slow" } });

        var test = await global::app.test.@this.From(TaggedGoal("slow"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
        await Assert.That(test.StatusReason?.ToString()).IsEqualTo("excluded by tag");
    }

    // Exclude set from the CLI shape (--test={"exclude":["slow"]}) binds and filters.
    [Test]
    public async Task From_ExcludeSetThroughTheWalk_Filters()
    {
        var set = _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["exclude"] = new List<object?> { "slow" } });
        await set.IsSuccess();

        var test = await global::app.test.@this.From(TaggedGoal("slow"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
        await Assert.That(test.StatusReason?.ToString()).IsEqualTo("excluded by tag");
    }

    [Test]
    public async Task From_TakenTest_IsReady()
    {
        _app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["exclude"] = new List<object?> { "slow" } });

        var test = await global::app.test.@this.From(TaggedGoal("fast"), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Ready);
        await Assert.That(test.StatusReason).IsNull();
    }

    // The include/exclude rules are the setting's options: the setting answers for a test.
    [Test]
    public async Task TheSetting_AnswersWhyItLeavesATestOut()
    {
        var context = _app.User.Context;
        var setting = new global::app.test.setting.@this();
        setting.Include.Add(new global::app.type.item.text.@this("fast"));
        var slow = new global::app.test.@this { Goal = TaggedGoal("slow") };
        slow.Tags.Add(new global::app.type.item.tag.@this("slow"));
        var fast = new global::app.test.@this { Goal = TaggedGoal("fast") };
        fast.Tags.Add(new global::app.type.item.tag.@this("fast"));

        await Assert.That(setting.Exclusion(slow, context)?.ToString()).IsEqualTo("no include match");
        await Assert.That(setting.Exclusion(fast, context)).IsNull();
    }

    // A test is born with the goals it reaches; the run's coverage takes them in when the run takes the test —
    // a skipped test's sites still show.
    [Test]
    public async Task TheRun_TakesInTheGoalsATestReaches()
    {
        var goal = Make.Goal("Covered",
            Make.Step("if %x% is 1", Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "=="), ("Right", 1))));
        goal.Tag.Add(new global::app.type.item.tag.@this("skip"));
        var test = await global::app.test.@this.From(goal, _app.User.Context);

        await _app.test.list.Start(new global::app.type.item.list.@this<global::app.test.@this>(new[] { test }), _app.User.Context);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
        await Assert.That(_app.test.list.Report.Coverage.BranchChains.Keys.Any(site => site.Contains("Covered"))).IsTrue();
    }
}
