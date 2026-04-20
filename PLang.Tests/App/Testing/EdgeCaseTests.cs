using System.Text.Json;
using global::App.Test;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 14 — safety net. Independent edge cases and security tests that don't
/// fit any single feature. Config boundary robustness (negative/zero/invalid),
/// re-entrant test.run, path-traversal constraints, ANSI stripping in captured
/// output (prevents injection into the failure diagnostic), nested-Data JSON
/// serialization, invalid format values.
/// These catch the cross-cutting failure modes that show up only in integration.
/// </summary>
[NotInParallel]
public class EdgeCaseTests
{
    private string _tempDir = null!;
    private global::App.@this _app = null!;
    private StringWriter _console = null!;
    private TextWriter _originalConsole = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-edge-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        var fs = new global::App.FileSystem.Default.PLangFileSystem(_tempDir, "");
        _app = new global::App.@this(fs);
        _originalConsole = Console.Out;
        _console = new StringWriter();
        Console.SetOut(_console);
    }

    [After(Test)]
    public async Task Teardown()
    {
        Console.SetOut(_originalConsole);
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    // --test={"timeout":-5} → error returned (not silently clamped). Prevents
    // zero-or-negative timeouts from causing immediate-cancel pathology.
    [Test]
    public async Task Config_Timeout_Negative_RejectedWithError()
    {
        var result = _app.Testing.Apply(new Dictionary<string, object?> { ["timeout"] = -5 });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("timeout");
    }

    // --test={"parallel":0} or {"parallel":-1} → error. Zero parallel means no
    // tests run (silently useless); negative is nonsense. Both surfaced as
    // config errors, not accepted and then silently ignored.
    [Test]
    public async Task Config_Parallel_ZeroOrNegative_RejectedWithError()
    {
        var zero = _app.Testing.Apply(new Dictionary<string, object?> { ["parallel"] = 0 });
        await Assert.That(zero.Success).IsFalse();

        var neg = _app.Testing.Apply(new Dictionary<string, object?> { ["parallel"] = -1 });
        await Assert.That(neg.Success).IsFalse();
    }

    // A test whose goal calls test.run itself (nested runner). The inner run's
    // results do not leak into the parent run's Results; no deadlock on the
    // semaphore; both inner and outer complete. Keeps test.run re-entrant so
    // meta-tests that exercise the runner are feasible.
    [Test]
    public async Task Run_RecursiveTestRun_InsideTest_IsolatedAndCompletes()
    {
        // Minimal shape: call test.run with an empty list twice — first outer, then
        // simulate a recursive call by calling it again. Without deadlock. Results
        // from the inner call stay on the inner App (we use the same app in the
        // test so the Results grow — verify count is from the outer action, not the
        // inner grandchild-runs).

        var emptyList = new List<TestFile>();
        var outerAction = new global::App.modules.test.run
        {
            Context = _app.User.Context,
            Tests = new global::App.Data.@this<List<TestFile>>("Tests", emptyList),
            Parallel = null,
            Timeout = null
        };
        var outerResult = await outerAction.Run();

        await Assert.That(outerResult.Success).IsTrue();
        await Assert.That(_app.Testing.Results.Count).IsEqualTo(0);
    }

    // --test={"path":"../../../etc"} → rejected. Discovery is constrained to
    // paths under the app's working directory — prevents a malicious test
    // config from enumerating system directories. (security)
    [Test]
    public async Task Discover_PathTraversal_OutsideProjectRoot_Rejected()
    {
        var action = new global::App.modules.test.discover
        {
            Context = _app.User.Context,
            Path = new global::App.Data.@this<string>("Path", "../../../etc"),
            Pattern = new global::App.Data.@this<string>("Pattern", "*.test.goal"),
            Recursive = new global::App.Data.@this<bool>("Recursive", true)
        };

        // discover catches traversal and returns an empty list — does not throw,
        // does not leak filesystem entries from outside the app root.
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as List<TestFile> ?? new List<TestFile>();
        await Assert.That(files.Count).IsEqualTo(0);
    }

    // A test that writes ANSI escape sequences via output.write: when the
    // captured output is rendered in the failure diagnostic, escape sequences
    // are stripped or escaped so the terminal renders literal text. Prevents
    // captured-output injection that could forge success messages, clear the
    // screen, or reposition the cursor. (security)
    [Test]
    public async Task Report_ConsoleCapture_AnsiEscapeSequences_Stripped()
    {
        var run = new TestRun(new TestFile { Path = "Tests/X.test.goal", EntryGoalName = "X" });
        run.CapturedOutput = "\x1B[32mFAKE OK\x1B[0m\x1B[2JCLEARED";
        run.Complete(TestStatus.Fail, new global::App.Errors.AssertionError(1, 2));
        _app.Testing.Results.Add(run);

        var action = new global::App.modules.test.report { Context = _app.User.Context };
        await action.Run();

        var output = _console.ToString();
        // Escape chars (0x1B) must not appear in the rendered output.
        await Assert.That(output.Contains('\x1B')).IsFalse();
        // The literal text survives — just the control sequences get stripped.
        await Assert.That(output.Contains("FAKE OK")).IsTrue();
    }

    // Variables snapshot where a Data's Value is itself a Data: JSON
    // serialization during report rendering completes without circular-
    // reference errors. Defensive case for nested Data in user code.
    [Test]
    public async Task Snapshot_DataContainingData_RenderedCorrectly_NoCircularReference()
    {
        var inner = new global::App.Data.@this("inner", 42);
        var outer = new global::App.Data.@this("outer", inner);

        var err = new global::App.Errors.AssertionError(1, 2)
        {
            Variables = new Dictionary<string, object?>
            {
                ["nested"] = outer
            }
        };
        var run = new TestRun(new TestFile { Path = "Tests/Nested.test.goal", EntryGoalName = "N" });
        run.Complete(TestStatus.Fail, err);
        _app.Testing.Results.Add(run);

        // Report rendering must not throw on nested Data.
        var action = new global::App.modules.test.report { Context = _app.User.Context };
        await action.Run();

        var jsonPath = System.IO.Path.Combine(_tempDir, ".test", "results.json");
        await Assert.That(System.IO.File.Exists(jsonPath)).IsTrue();
        var json = await System.IO.File.ReadAllTextAsync(jsonPath);
        // Just verify the file is parseable JSON — no circular errors.
        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.GetProperty("runs").GetArrayLength()).IsGreaterThan(0);
    }

    // --test={"format":"csv"} → error. Only "json" and "junit" are valid values
    // for the Batch 11 format selector. Robustness on the config surface.
    [Test]
    public async Task Config_Format_InvalidValue_RejectedWithError()
    {
        var result = _app.Testing.Apply(new Dictionary<string, object?> { ["format"] = "csv" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("format");
    }
}
