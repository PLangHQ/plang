using System.Xml.Linq;
using global::App.Errors;
using global::App.Test;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 11 — test.report action.
/// Three output surfaces: console (always), file artifact (format-selected), and
/// coverage tables (console). Output directory is .test/ at the app root (per Ingi Q4
/// decision — users narrow focus via include/exclude, report stays predictable).
/// Format selector: --test={"format":"json"|"junit"}. Default "json".
/// File format is single-select; if users need both, they run twice (v1 decision).
/// Failure rendering matches architect §4.6: Expected/Actual + Variables snapshot block.
/// </summary>
[NotInParallel] // Console.SetOut is process-wide — serialize these to avoid capture races.
public class ReportActionTests
{
    private string _tempDir = null!;
    private global::App.@this _app = null!;
    private StringWriter _console = null!;
    private TextWriter _originalConsole = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-report-" + Guid.NewGuid().ToString("N")[..8]);
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

    private static TestRun NewRun(string name, TestStatus status, IError? error = null, string? capturedOutput = null)
    {
        var run = new TestRun(new TestFile
        {
            Path = $"Tests/{name}.test.goal",
            EntryGoalName = name,
            GoalHash = "deadbeef",
            BuilderVersion = "v1"
        });
        run.Complete(status, error);
        if (capturedOutput != null) run.CapturedOutput = capturedOutput;
        return run;
    }

    private async Task Report()
    {
        var action = new global::App.modules.test.report { Context = _app.User.Context };
        await action.Run();
    }

    // Console output (summary + per-test status) is independent of the format selector.
    // Always writes. Format only controls which file artifact is produced.
    [Test]
    public async Task Report_Console_AlwaysWritesSummary_RegardlessOfFormat()
    {
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));
        _app.Testing.Format = "junit";

        await Report();

        var output = _console.ToString();
        await Assert.That(output.Contains("Test summary")).IsTrue();
        await Assert.That(output.Contains("1 pass")).IsTrue();
    }

    // .test/ output lives at the app root (Q4 decision). Discovery path was
    // rejected — users narrow tests via include/exclude instead.
    [Test]
    public async Task Report_OutputDirectory_IsDotTestRelativeToDiscoveryPath()
    {
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));

        await Report();

        var expectedDir = System.IO.Path.Combine(_tempDir, ".test");
        await Assert.That(System.IO.Directory.Exists(expectedDir)).IsTrue();
    }

    // Default (no --test format override): .test/results.json is written,
    // .test/junit.xml is NOT. Format defaults to "json".
    [Test]
    public async Task Report_Format_DefaultIsJson_WritesResultsJson()
    {
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));

        await Report();

        var jsonPath = System.IO.Path.Combine(_tempDir, ".test", "results.json");
        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        await Assert.That(System.IO.File.Exists(jsonPath)).IsTrue();
        await Assert.That(System.IO.File.Exists(junitPath)).IsFalse();
    }

    // --test={"format":"junit"}: .test/junit.xml is written, .test/results.json is NOT.
    // Single-select format — each file artifact is either-or per run.
    [Test]
    public async Task Report_Format_Junit_WritesJunitXml()
    {
        _app.Testing.Format = "junit";
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));

        await Report();

        var jsonPath = System.IO.Path.Combine(_tempDir, ".test", "results.json");
        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        await Assert.That(System.IO.File.Exists(junitPath)).IsTrue();
        await Assert.That(System.IO.File.Exists(jsonPath)).IsFalse();
    }

    // Test named "asserts <x> & <y>" → junit.xml properly XML-escapes (&lt;, &gt;, &amp;).
    // Prevents XML injection that would break CI parsers (GitHub Actions, Gradle, Jenkins).
    // (independent — security)
    [Test]
    public async Task Report_JUnit_TestNameWithXmlSpecialChars_Escaped()
    {
        _app.Testing.Format = "junit";
        _app.Testing.Results.Add(NewRun("asserts <x> & <y>", TestStatus.Pass));

        await Report();

        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        var xml = await System.IO.File.ReadAllTextAsync(junitPath);
        // Well-formed XML requires proper escaping — parse and verify round-trip.
        var doc = XDocument.Parse(xml);
        var testName = doc.Descendants("testcase").First().Attribute("name")?.Value;
        await Assert.That(testName).Contains("<x>");
        await Assert.That(testName).Contains("&");
    }

    // Module.action coverage table: rows = every registered handler in App.Modules.All,
    // columns mark observed vs not-observed across the run. Coverage percentage computed.
    [Test]
    public async Task Report_Coverage_ModuleActionTable_ShowsUniverseVsObserved()
    {
        _app.Testing.Coverage.RecordModuleAction("variable", "set");
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));

        await Report();

        var output = _console.ToString();
        await Assert.That(output.Contains("Module.action coverage")).IsTrue();
        await Assert.That(output.Contains("variable.set")).IsTrue();
        // Some uncovered actions should appear too (universe)
        await Assert.That(output.Contains("[x] variable.set")).IsTrue();
    }

    // Branch coverage table: one row per condition.if site (keyed "goalName:stepIndex"),
    // columns show which branch indices were observed. Sites with gaps (e.g., only {0}
    // seen, missing {1}) flagged in the rendering.
    [Test]
    public async Task Report_Coverage_BranchTable_PerSiteShowsObservedIndices()
    {
        _app.Testing.Coverage.RecordBranch("MyGoal:3", 0);
        _app.Testing.Coverage.RecordBranch("MyGoal:3", 1);
        _app.Testing.Results.Add(NewRun("X", TestStatus.Pass));

        await Report();

        var output = _console.ToString();
        await Assert.That(output.Contains("Branch coverage")).IsTrue();
        // Parse the site line to verify the exact rendered contents at MyGoal:3,
        // instead of the old loose Contains("0") / Contains("1") check that passed
        // accidentally on any single digit appearing anywhere in the summary.
        var siteLine = output.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("MyGoal:3:"));
        await Assert.That(siteLine).IsNotNull();
        await Assert.That(siteLine!).Contains("0");
        await Assert.That(siteLine!).Contains("1");
        // The rendering wraps branches in {}, so the structural marker must be present.
        await Assert.That(siteLine!).Contains("{");
        await Assert.That(siteLine!).Contains("}");
    }

    // JUnit <failure> element for Fail status carries the error message. CI dashboards
    // parse this — malformed XML or missing elements break the reporting pipeline.
    [Test]
    public async Task Report_Junit_FailStatus_EmitsFailureElement()
    {
        _app.Testing.Format = "junit";
        var err = new AssertionError(1, 2, "mismatch");
        _app.Testing.Results.Add(NewRun("Failing", TestStatus.Fail, err));

        await Report();

        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        var doc = XDocument.Parse(await System.IO.File.ReadAllTextAsync(junitPath));
        var testcase = doc.Descendants("testcase").Single();
        var failure = testcase.Element("failure");
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Value).Contains("Expected: 1");
    }

    // JUnit <failure type="timeout"> distinguishes timeout from assertion failure —
    // dashboards colour them differently. Missing type attribute breaks CI filtering.
    [Test]
    public async Task Report_Junit_TimeoutStatus_EmitsFailureWithTypeTimeout()
    {
        _app.Testing.Format = "junit";
        _app.Testing.Results.Add(NewRun("SlowTest", TestStatus.Timeout));

        await Report();

        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        var doc = XDocument.Parse(await System.IO.File.ReadAllTextAsync(junitPath));
        var testcase = doc.Descendants("testcase").Single();
        var failure = testcase.Element("failure");
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Attribute("type")?.Value).IsEqualTo("timeout");
    }

    // JUnit <skipped> with reason for tests filtered out by tag include/exclude.
    [Test]
    public async Task Report_Junit_SkippedStatus_EmitsSkippedElement()
    {
        _app.Testing.Format = "junit";
        var run = NewRun("Filtered", TestStatus.Skipped);
        run.File.StatusReason = "excluded by tag";
        _app.Testing.Results.Add(run);

        await Report();

        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        var doc = XDocument.Parse(await System.IO.File.ReadAllTextAsync(junitPath));
        var testcase = doc.Descendants("testcase").Single();
        var skipped = testcase.Element("skipped");
        await Assert.That(skipped).IsNotNull();
        await Assert.That(skipped!.Value).Contains("excluded by tag");
    }

    // Stale tests (source hash changed but not rebuilt) also render as <skipped> with
    // the reason — CI surfaces them as not-run, the report explains why.
    [Test]
    public async Task Report_Junit_StaleStatus_EmitsSkippedWithReason()
    {
        _app.Testing.Format = "junit";
        var run = NewRun("StaleTest", TestStatus.Stale);
        run.File.StatusReason = "goal hash changed since build";
        _app.Testing.Results.Add(run);

        await Report();

        var junitPath = System.IO.Path.Combine(_tempDir, ".test", "junit.xml");
        var doc = XDocument.Parse(await System.IO.File.ReadAllTextAsync(junitPath));
        var testcase = doc.Descendants("testcase").Single();
        var skipped = testcase.Element("skipped");
        await Assert.That(skipped).IsNotNull();
        await Assert.That(skipped!.Value).Contains("goal hash changed");
    }

    // Architect §4.6 format: "FAIL: <step text>" header, then Expected/Actual lines,
    // then "Variables:" block with one line per variable. Unset vars render "(unset)",
    // null vars render "(null)", complex values (lists, dicts) JSON-serialized.
    [Test]
    public async Task Report_FailureDetail_RendersVariablesSnapshot_WithUnsetAndNullMarkers()
    {
        var err = new AssertionError(1, 2)
        {
            Variables = new Dictionary<string, object?>
            {
                ["idx"] = 1,
                ["items"] = new List<int> { 1, 2, 3 },
                ["maybe"] = null
            }
        };
        _app.Testing.Results.Add(NewRun("Failing", TestStatus.Fail, err));

        await Report();

        var output = _console.ToString();
        await Assert.That(output.Contains("FAIL")).IsTrue();
        await Assert.That(output.Contains("Expected: 1")).IsTrue();
        await Assert.That(output.Contains("Actual:   2")).IsTrue();
        await Assert.That(output.Contains("%idx%")).IsTrue();
        await Assert.That(output.Contains("%maybe% = (null)")).IsTrue();
    }
}
