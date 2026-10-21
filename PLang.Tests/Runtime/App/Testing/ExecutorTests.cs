using global::PLang;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Coverage for PLang.Executor.Configure — the CLI argv parsing and routing layer.
/// The previous test surface covered Testing.Apply / Debug.Apply directly with
/// dictionaries, but the argv → CommandLineParser → engine-state pipeline was 0%.
/// Mis-mapped flags (e.g. --test silently not enabling, --debug= value not reaching
/// Debug.Apply) would ship without a test catching them.
///
/// Tests exercise Configure() — the split that returns the configured engine without
/// executing Start(). Run() is Configure() + Start(), so covering Configure covers the
/// entire argv-to-engine-state path.
/// </summary>
public class ExecutorTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-executor-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Teardown()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private Executor NewExecutor() => new(_tempDir);

    // --test flag turns on test mode and routes to /system/test.goal when the
    // default Start.goal is the target. Users expect `plang --test` to run the test
    // runner, not Start.goal.
    [Test]
    public async Task Configure_TestFlag_SetsTestingIsEnabled_RoutesToSystemTestPr()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "Start.goal", "--test" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.test.list.Session != null).IsTrue();
        await Assert.That((string?)await engine.actor.list.System.Context.Variable.GetValue("goalFile"))
            .IsEqualTo("/system/test.goal");
        await using var _ = engine;
    }

    // --test.setting={"timeout":5} routes through Testing.Apply with the parsed dict and sets
    // Timeout. Exercises the CommandLineParser JSON collection path.
    [Test]
    public async Task Configure_TestFlagWithConfig_AppliesToTesting()
    {
        var executor = NewExecutor();
        // Keys are property names — the setting walk maps them (no "timeout" alias).
        var (engine, error) = await executor.Configure(new[] { "--test.setting={\"timeout\":\"5s\",\"parallel\":3}" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.test.list.Session != null).IsTrue();
        await Assert.That(engine.actor.list.System.Context.Setting.Of<global::app.test.setting.@this>().Timeout.Value.TotalSeconds).IsEqualTo(5);
        await Assert.That(engine.actor.list.System.Context.Setting.Of<global::app.test.setting.@this>().Parallel).IsEqualTo(3);
        await using var _ = engine;
    }

    // --test with invalid config (a value outside a closed type — an unknown format) surfaces
    // as an error Data from Configure. Run() propagates it as the final result without Start().
    // (Negative timeout/parallel is NOT invalid any more — it's a sentinel; see EdgeCaseTests.)
    [Test]
    public async Task Configure_TestFlagWithInvalidConfig_ReturnsApplyError()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "--test.setting.format=csv" });

        await Assert.That(error).IsNotNull();
        await error!.IsFailure();
        await Assert.That(engine).IsNull();
    }

    // --debug=Start routes to Debug.Apply which sets IsEnabled and parses the argument.
    // A string value (vs dict) is accepted — scalar values set the IsEnabled bit only.
    [Test]
    public async Task Configure_DebugFlag_InvokesDebugApply()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "Start.goal", "--debug=Start" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Debug != null).IsTrue();
        await using var _ = engine;
    }

    // --build makes the app's build; %!build.setting.cache% is then the running build's settings (the app's
    // member answers before the module). Default cache flag is the class's default.
    [Test]
    public async Task Configure_BuildFlag_SetsBuildingIsEnabled_SyncsCacheVar()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "--build" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Build != null).IsTrue();
        // %!build.setting.cache% — what the builder's goals hand llm.query (`cache=%!build.setting.cache%`)
        var cacheVar = await new global::app.type.item.variable.@this("!build.setting.cache", engine.type.list).Start(engine.actor.list.User.Context);
        await Assert.That((await cacheVar.Value())?.ToString()).IsEqualTo("use");
        await using var _ = engine;
    }

    // --build.setting.cache=skip reaches %!build.setting.cache% — llm.query in the builder's goals gets skip.
    [Test]
    public async Task Configure_BuildCacheSkip_ReachesBuildCacheSetting()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "--build.setting.cache=skip" });

        await Assert.That(error).IsNull();
        await using var _ = engine!;
        var cacheVar = await new global::app.type.item.variable.@this("!build.setting.cache", engine!.type.list).Start(engine!.actor.list.User.Context);
        await Assert.That((await cacheVar.Value())?.ToString()).IsEqualTo("skip");
    }

    // Positional "build" arg is normalized to --build — equivalent invocation.
    [Test]
    public async Task Configure_PositionalBuild_NormalizedToBuildFlag()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "build" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Build != null).IsTrue();
        await using var _ = engine;
    }

    // CLI parameters (non-!, non-system) are injected as user Variables. `name=my-app`
    // ends up as %name% = "my-app" accessible from PLang code.
    [Test]
    public async Task Configure_CliParameters_InjectedIntoUserVariables()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "Start.goal", "count=42", "label=hello" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        var vars = engine!.actor.list.User.Context.Variable;
        await Assert.That((long)(await vars.GetValue("count"))!).IsEqualTo(42L);
        await Assert.That((string?)await vars.GetValue("label")).IsEqualTo("hello");
        await using var _ = engine;
    }

    // No special flags: goalFile is the positional arg's .goal (the goal finds its own .pr).
    // Test mode is disabled, Debug is disabled, Building is disabled.
    [Test]
    public async Task Configure_NoSpecialFlags_RoutesToGoalPrPath()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "Start.goal" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.test.list.Session != null).IsFalse();
        await Assert.That(engine.Debug != null).IsFalse();
        await Assert.That(engine.Build != null).IsFalse();
        await Assert.That((string?)await engine.actor.list.System.Context.Variable.GetValue("goalFile"))
            .IsEqualTo("/Start.goal");
        await using var _ = engine;
    }

    // A goal in a subfolder runs from that folder's own .build (sub/.build/run.pr), not the root's.
    [Test]
    public async Task Configure_ASubfolderGoal_IsThatGoal()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "sub/Run" });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        var goalFile = (string?)await engine!.actor.list.System.Context.Variable.GetValue("goalFile");
        await Assert.That(goalFile).IsEqualTo("/sub/Run.goal");
        await Assert.That(global::app.goal.@this.Pr(global::app.type.item.path.@this.Resolve(goalFile!, engine.actor.list.System.Context)).ToString())
            .IsEqualTo("/sub/.build/run.pr");
    }

    // --test AND --debug can compose: test mode on, debug handlers attached.
    [Test]
    public async Task Configure_TestAndDebugFlags_BothApplied()
    {
        var executor = NewExecutor();
        var (engine, error) = await executor.Configure(new[] { "--test", "--debug=Start" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.test.list.Session != null).IsTrue();
        await Assert.That(engine.Debug != null).IsTrue();
        await using var _ = engine;
    }

    // In test mode the positional is what to test, wherever it stands: --test module/math and module/math --test both
    // test module/math (a path= given still wins).
    [Test]
    [Arguments(new[] { "--test", "module/math" }, "module/math")]
    [Arguments(new[] { "module/math", "--test" }, "module/math")]
    [Arguments(new[] { "--test", "module/math/AddRoundTripDecimal.test.goal" }, "module/math/AddRoundTripDecimal.test.goal")]
    [Arguments(new[] { "--test", "path=module/list", "module/math" }, "module/list")]
    public async Task Configure_TestWithAPositional_TestsThatPath(string[] args, string tested)
    {
        var (engine, error) = await NewExecutor().Configure(args);
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.test.list.Session != null).IsTrue();
        await Assert.That((string?)await engine.actor.list.System.Context.Variable.GetValue("goalFile")).IsEqualTo("/system/test.goal");
        await Assert.That((await engine.actor.list.User.Context.Variable.GetValue("path"))?.ToString()).EndsWith(tested);
    }

    // Run mode: the positional is the goal to run, wherever it stands.
    [Test]
    public async Task Configure_APositionalAfterAFlag_IsTheGoalToRun()
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "--call.setting.timing=true", "sub/Run" });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That((string?)await engine!.actor.list.System.Context.Variable.GetValue("goalFile")).IsEqualTo("/sub/Run.goal");
    }

    // The command line mirrors the path: --<path>=<value> is this run's value for %!<path>%, `app.` optional, or a dict
    // at the setting's own path for several options at once.
    [Test]
    [Arguments("--app.channel.setting.format=bin")]
    [Arguments("--channel.setting.format=bin")]
    [Arguments("--app.channel.setting={\"format\":\"bin\"}")]
    [Arguments("--channel.setting={\"format\":\"bin\"}")]
    public async Task Configure_ASettingPath_IsThisRunsValue(string flag)
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "Start.goal", flag });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.actor.list.User.Context.Setting.Of<global::app.channel.setting.@this>().Format.ToString())
            .IsEqualTo("application/octet-stream");
    }

    // A path that names no setting, or no option of the setting it names, is refused — as a step's set is.
    [Test]
    [Arguments("--channel.setting.fromat=bin", "channel.setting")]
    [Arguments("--nothing.here=1", "nothing.here")]
    [Arguments("--callstack={\"timing\":true}", "callstack")]
    public async Task Configure_APathNoSettingAnswers_IsRefused(string flag, string named)
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "Start.goal", flag });

        await Assert.That(engine).IsNull();
        await error!.IsFailure();
        await Assert.That(error.Error!.Message).Contains(named);
    }

    // A mode word takes no settings: they are written at its setting's path.
    [Test]
    public async Task Configure_AModeWordWithADict_IsRefused_NamingItsSettingsPath()
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "--debug={\"goal\":\"Start\"}" });

        await Assert.That(engine).IsNull();
        await error!.IsFailure();
        await Assert.That(error.Error!.Message).Contains("--debug.setting");
    }

    // A mode is on when the run writes its setting: --debug.setting.goal=Start alone debugs, as the bare word does.
    [Test]
    public async Task Configure_ADebugSettingAlone_SwitchesDebugOn()
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "Start.goal", "--debug.setting.goal=Start" });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.Debug != null).IsTrue();
        await Assert.That(engine.Debug!.Setting.Goal?.ToString()).IsEqualTo("Start");
    }

    [Test]
    public async Task Configure_ATestSettingAlone_SwitchesTestOn()
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "--test.setting.parallel=3" });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.test.list.Session != null).IsTrue();
    }

    [Test]
    public async Task Configure_ABuildSettingAlone_SwitchesBuildOn()
    {
        var (engine, error) = await NewExecutor().Configure(new[] { "--build.setting.cache=skip" });
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.Build != null).IsTrue();
    }

    // A debug run times its frames, unless the command line says otherwise.
    [Test]
    [Arguments(new[] { "--debug" }, true)]
    [Arguments(new[] { "--debug", "--call.setting.timing=false" }, false)]
    public async Task Configure_ADebugRun_TimesItsFrames_UnlessTheCommandLineSaysNot(string[] flags, bool timed)
    {
        var (engine, error) = await NewExecutor().Configure(flags);
        await using var _ = engine;

        await Assert.That(error).IsNull();
        await Assert.That(engine!.actor.list.System.Context.Setting.Of<global::app.call.setting.@this>().Timing.Value).IsEqualTo(timed);
    }

    // Covers Start()'s composition with Configure(): an invalid --test config produces
    // an error from Configure, and Start must propagate that error without calling
    // engine.Start() (no .build/start.pr exists in the fixture filesystem, so if
    // Start were invoked it would return a file-not-found error instead of the
    // Apply error). The assertion on the error Key differentiates the two paths.
    [Test]
    public async Task Run_InvalidConfig_ReturnsErrorWithoutStarting()
    {
        var executor = NewExecutor();
        // An unknown format value is rejected by the choice<Format> conversion in the walk.
        var result = await executor.Start(new[] { "--test.setting.format=csv" });

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
    }
}
