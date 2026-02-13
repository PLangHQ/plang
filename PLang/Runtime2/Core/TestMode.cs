using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

/// <summary>
/// Test runner for PLang. Discovers *.test.goal files, runs them,
/// tracks assertion pass/fail via events, and prints a summary.
/// Activated by: plang p !test
/// </summary>
public static class TestMode
{
    private sealed class TestResult
    {
        public string FilePath { get; init; } = "";
        public List<AssertionFailure> Failures { get; } = new();
        public bool Passed => Failures.Count == 0;
        public bool Errored { get; set; }
        public string? ErrorMessage { get; set; }
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
    public static async Task<int> RunAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        var fileSystem = engine.FileSystem;
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
                await RunSingleTest(fileSystem, rootDir, testFile, result, cancellationToken);
                Console.WriteLine(result.Passed && !result.Errored ? " PASS" : " FAIL");
            }
            catch (Exception ex)
            {
                result.Errored = true;
                result.ErrorMessage = ex.Message;
                Console.WriteLine(" ERROR");
            }

            results.Add(result);
        }

        PrintSummary(results);
        return results.Any(r => !r.Passed || r.Errored) ? 1 : 0;
    }

    private static async Task RunSingleTest(
        Interfaces.IPLangFileSystem fileSystem,
        string rootDir,
        string testFile,
        TestResult result,
        CancellationToken cancellationToken)
    {
        // Derive the .pr file path
        var dir = fileSystem.Path.GetDirectoryName(testFile) ?? "";
        var fileName = fileSystem.Path.GetFileNameWithoutExtension(testFile);
        if (fileName.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^5];

        var prPath = fileSystem.Path.Combine(dir, ".build", fileName.ToLowerInvariant() + ".test.pr");
        if (!fileSystem.File.Exists(prPath))
        {
            result.Errored = true;
            result.ErrorMessage = "Built .pr file not found. Run 'plang p build' first.";
            return;
        }

        // Fresh engine with the same root as the original engine.
        // Goal resolution uses Goal.FolderPath for relative lookups,
        // so the engine root stays at the top level (e.g., Tests/Runtime2/).
        var testFs = new SafeFileSystem.PLangFileSystem(rootDir, "");
        await using var testEngine = new Engine(testFs);
        testEngine.IsTestMode = true;

        // Load the test .pr file
        await testEngine.Goals.LoadFromFileAsync(testEngine, prPath, cancellationToken: cancellationToken);

        var testGoalName = "Start";
        var goal = testEngine.Goals.Get(testGoalName);
        if (goal == null)
        {
            result.Errored = true;
            result.ErrorMessage = $"Goal '{testGoalName}' not found in {prPath}";
            return;
        }

        // Register assertion failure tracking
        var events = testEngine.Context.User.Events;
        events.Register(
            EventType.AfterStep,
            context => TrackAssertionFailures(context, result),
            goalNamePattern: "*",
            priority: int.MaxValue - 1,
            stopOnError: false
        );

        var runResult = await testEngine.RunGoalAsync(goal, cancellationToken: cancellationToken);

        if (!runResult.Success && runResult.Error is not AssertionError)
        {
            if (result.Failures.Count == 0)
            {
                result.Errored = true;
                result.ErrorMessage = runResult.Error?.Message ?? "Goal execution failed";
            }
        }
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
        var passed = results.Count(r => r.Passed && !r.Errored);
        var failed = results.Count(r => !r.Passed || r.Errored);
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

            foreach (var result in results.Where(r => !r.Passed || r.Errored))
            {
                Console.WriteLine($"  FAILED: {result.FilePath}");

                if (result.Errored)
                {
                    Console.WriteLine($"    Error: {result.ErrorMessage}");
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
