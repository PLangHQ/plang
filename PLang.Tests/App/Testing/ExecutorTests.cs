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

    // --test flag turns on test mode and routes to system/.build/test.pr when the
    // default Start.goal is the target. Users expect `plang --test` to run the test
    // runner, not Start.goal.
    [Test]
    public async Task Configure_TestFlag_SetsTestingIsEnabled_RoutesToSystemTestPr()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "Start.goal", "--test" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Tester.IsEnabled).IsTrue();
        await Assert.That(engine.System.Context.Variable.Get<string>("goalFile"))
            .IsEqualTo("/system/.build/test.pr");
        await using var _ = engine;
    }

    // --test={"timeout":5} routes through Testing.Apply with the parsed dict and sets
    // TimeoutSeconds. Exercises the CommandLineParser JSON collection path.
    [Test]
    public async Task Configure_TestFlagWithConfig_AppliesToTesting()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "--test={\"timeout\":5,\"parallel\":3}" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Tester.IsEnabled).IsTrue();
        await Assert.That(engine.Tester.TimeoutSeconds).IsEqualTo(5);
        await Assert.That(engine.Tester.Parallel).IsEqualTo(3);
        await using var _ = engine;
    }

    // --test with invalid config (negative timeout) surfaces as an error Data returned
    // from Configure. Run() propagates it as the final result without calling Start().
    [Test]
    public async Task Configure_TestFlagWithInvalidConfig_ReturnsApplyError()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "--test={\"timeout\":-1}" });

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Success).IsFalse();
        await Assert.That(engine).IsNull();
    }

    // --debug=Start routes to Debug.Apply which sets IsEnabled and parses the argument.
    // A string value (vs dict) is accepted — scalar values set the IsEnabled bit only.
    [Test]
    public async Task Configure_DebugFlag_InvokesDebugApply()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "Start.goal", "--debug=Start" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Debug.IsEnabled).IsTrue();
        await using var _ = engine;
    }

    // --build sets Building.IsEnabled and syncs the !build.cache variable so the
    // PLang builder's Build.goal reads it. Default cache flag is Building.Cache's default.
    [Test]
    public async Task Configure_BuildFlag_SetsBuildingIsEnabled_SyncsCacheVar()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "--build" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Builder.IsEnabled).IsTrue();
        var cacheVar = engine.User.Context.Variable.Get("!build.cache");
        await Assert.That(cacheVar).IsNotNull();
        await using var _ = engine;
    }

    // Positional "build" arg is normalized to --build — equivalent invocation.
    [Test]
    public async Task Configure_PositionalBuild_NormalizedToBuildFlag()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "build" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Builder.IsEnabled).IsTrue();
        await using var _ = engine;
    }

    // CLI parameters (non-!, non-system) are injected as user Variables. `name=my-app`
    // ends up as %name% = "my-app" accessible from PLang code.
    [Test]
    public async Task Configure_CliParameters_InjectedIntoUserVariables()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "Start.goal", "count=42", "label=hello" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        var vars = engine!.User.Context.Variable;
        await Assert.That(vars.Get<long>("count")).IsEqualTo(42L);
        await Assert.That(vars.Get<string>("label")).IsEqualTo("hello");
        await using var _ = engine;
    }

    // No special flags: goalFile is computed from the positional arg, routed into
    // .build/start.pr. Test mode is disabled, Debug is disabled, Building is disabled.
    [Test]
    public async Task Configure_NoSpecialFlags_RoutesToGoalPrPath()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "Start.goal" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Tester.IsEnabled).IsFalse();
        await Assert.That(engine.Debug.IsEnabled).IsFalse();
        await Assert.That(engine.Builder.IsEnabled).IsFalse();
        await Assert.That(engine.System.Context.Variable.Get<string>("goalFile"))
            .IsEqualTo("/.build/start.pr");
        await using var _ = engine;
    }

    // --test AND --debug can compose: test mode on, debug handlers attached.
    [Test]
    public async Task Configure_TestAndDebugFlags_BothApplied()
    {
        var executor = NewExecutor();
        var (engine, error) = executor.Configure(new[] { "--test", "--debug=Start" });

        await Assert.That(error).IsNull();
        await Assert.That(engine).IsNotNull();
        await Assert.That(engine!.Tester.IsEnabled).IsTrue();
        await Assert.That(engine.Debug.IsEnabled).IsTrue();
        await using var _ = engine;
    }

    // Covers Run()'s composition with Configure(): an invalid --test config produces
    // an error from Configure, and Run must propagate that error without calling
    // engine.Start() (no .build/start.pr exists in the fixture filesystem, so if
    // Start were invoked it would return a file-not-found error instead of the
    // Apply error). The assertion on the error Key differentiates the two paths.
    [Test]
    public async Task Run_InvalidConfig_ReturnsErrorWithoutStarting()
    {
        var executor = NewExecutor();
        var result = await executor.Run(new[] { "--test={\"timeout\":-1}" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidTestConfig");
    }
}
