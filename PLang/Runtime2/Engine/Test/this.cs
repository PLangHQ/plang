using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;
using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

namespace PLang.Runtime2.Engine.Test;

/// <summary>
/// Test runner for PLang. Discovers *.test.goal files, runs them,
/// tracks assertion pass/fail via events, and prints a summary.
/// Activated by: plang p !test
/// </summary>
public sealed class @this
{
    private readonly Engine.@this _engine;

    /// <summary>
    /// Whether test mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    public @this(Engine.@this engine)
    {
        _engine = engine;
    }

    private sealed class TestResult
    {
        public string FilePath { get; init; } = "";
        public List<AssertionFailure> Failures { get; } = new();
        public Data Result { get; set; } = Data.Ok();
        public bool Passed => Result.Success && Failures.Count == 0;
    }

    private sealed class AssertionFailure
    {
        public int StepIndex { get; init; }
        public string StepText { get; init; } = "";
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Discovers and runs all *.test.goal files, prints summary, returns exit code.
    /// Each test file gets a fresh engine for full isolation.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        IsEnabled = true;

        var fileSystem = _engine.FileSystem;
        var rootDir = fileSystem.RootDirectory;

        // Discover all *.test.goal files
        var testFiles = fileSystem.Directory.GetFiles(rootDir, "*.test.goal", SearchOption.AllDirectories);

        if (testFiles.Length == 0)
        {
            Console.WriteLine("No test files found (*.test.goal)");
            return 0;
        }

        Console.WriteLine($"Discovered {testFiles.Length} test file(s)");
        Console.WriteLine();

        var results = new List<TestResult>();

        foreach (var testFile in testFiles)
        {
            var relativePath = fileSystem.Path.GetRelativePath(rootDir, testFile);
            var result = new TestResult { FilePath = relativePath };

            Console.Write($"  Running {relativePath}...");

            try
            {
                result.Result = await RunSingleTest(fileSystem, rootDir, testFile, result, _engine.Debug.IsEnabled, cancellationToken);
                Console.WriteLine(result.Passed ? " PASS" : " FAIL");
            }
            catch (Exception ex)
            {
                result.Result = Data.FromError(Error.FromException(ex));
                Console.WriteLine(" ERROR");
            }

            results.Add(result);
        }

        PrintSummary(results);
        return results.Any(r => !r.Passed) ? 1 : 0;
    }

    private static async Task<Data> RunSingleTest(
        Interfaces.IPLangFileSystem fileSystem,
        string rootDir,
        string testFile,
        TestResult result,
        bool debug,
        CancellationToken cancellationToken)
    {
        // Derive the .pr file path
        var dir = fileSystem.Path.GetDirectoryName(testFile) ?? "";
        var fileName = fileSystem.Path.GetFileNameWithoutExtension(testFile);
        if (fileName.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^5];

        var prPath = fileSystem.Path.Combine(dir, ".build", fileName.ToLowerInvariant() + ".test.pr");
        if (!fileSystem.File.Exists(prPath))
            return Data.FromError(new ServiceError("PrNotFound", "Built .pr file not found. Run 'plang p build' first.", 404));

        // Each test folder is its own PLang app root.
        // Setup discovery and goal resolution work relative to this root.
        var testFs = new SafeFileSystem.PLangFileSystem(dir, "");
        await using var testEngine = new Engine.@this(testFs);
        testEngine.Testing.IsEnabled = true;
        if (debug) testEngine.Debug.Apply(true);

        // Load the test .pr file
        await testEngine.Goals.LoadFromFileAsync(testEngine, prPath, cancellationToken: cancellationToken);

        // Run setup goals (e.g., create DB tables) before the test
        var setupResult = await testEngine.Goals.Setup.RunAsync(testEngine, testEngine.User.Context, cancellationToken);
        if (!setupResult.Success) return setupResult;

        var testGoalName = "Start";
        var goal = testEngine.Goals.Get(testGoalName);
        if (goal == null)
            return Data.FromError(new ServiceError("GoalNotFound", $"Goal '{testGoalName}' not found in {prPath}", 404));

        // Register assertion failure tracking
        var events = testEngine.Context.User.Events;
        events.Register(new EventBinding(
            EventType.AfterStep,
            context => TrackAssertionFailures(context, result),
            goalNamePattern: "*",
            priority: int.MaxValue - 1,
            stopOnError: false));

        return await testEngine.RunGoalAsync(goal, cancellationToken: cancellationToken);
    }

    private static Task<Data> TrackAssertionFailures(PLangContext context, TestResult result)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(Data.Ok());

        var lastResult = context.MemoryStack.Get("__stepResult");
        if (lastResult != null && !lastResult.Success && lastResult.Error is AssertionError assertError)
        {
            result.Failures.Add(new AssertionFailure
            {
                StepIndex = step.Index,
                StepText = step.Text,
                Message = assertError.Message
            });
        }

        return Task.FromResult(Data.Ok());
    }

    private static void PrintSummary(List<TestResult> results)
    {
        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);
        var total = results.Count;

        Console.WriteLine();
        Console.WriteLine("========================================");

        if (failed == 0)
        {
            Console.WriteLine($"Test run summary: Passed!");
            Console.WriteLine($"  total: {total} suite(s)");
            Console.WriteLine($"  passed: {passed}");
            Console.WriteLine($"  failed: 0");
        }
        else
        {
            Console.WriteLine($"Test run summary: {passed} passed, {failed} failed, {total} total");
            Console.WriteLine();

            foreach (var result in results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  FAILED: {result.FilePath}");

                if (!result.Result.Success)
                {
                    Console.WriteLine($"    Error: {result.Result.Error?.Message}");
                }

                foreach (var failure in result.Failures)
                {
                    Console.WriteLine($"    [{failure.StepIndex}] {failure.StepText}");
                    Console.WriteLine($"        {failure.Message}");
                }

                Console.WriteLine();
            }
        }

        Console.WriteLine("========================================");
    }
}
