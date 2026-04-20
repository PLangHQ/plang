using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.Errors;
using App.Test;
using App.Variables;

namespace App.modules.test;

/// <summary>
/// Writes the run-wide Results to the console (always) and to a single file artefact
/// (either .test/results.json or .test/junit.xml, selected by Testing.Format).
/// Console output: summary line + per-test status, with a failure block that includes
/// Expected/Actual and the Variables snapshot captured on AssertionError.
/// Also emits two coverage tables: module.action universe vs observed, and per-site
/// branch-index coverage for condition.if. The .test/ directory lives at the app root
/// (users scope a run with Testing.Include/Exclude — output location stays predictable).
/// </summary>
[Example("write test report %results%",
    "Results=%results%")]
[Action("report", Cacheable = false)]
public partial class report : IContext
{
    public partial Data.@this<App.Test.Results>? Results { get; init; }

    public async Task<Data.@this> Run()
    {
        var results = Results?.Value ?? Context.App!.Testing.Results;
        var testing = Context.App!.Testing;

        var console = new StringBuilder();
        RenderConsole(console, results, testing);
        RenderCoverageTables(console, testing, Context.App.Modules);
        Console.Out.Write(console.ToString());

        // Write the file artefact. .test/ lives at the app root per Q4 decision.
        var fs = Context.App.FileSystem;
        var outDir = fs.Path.Combine(fs.RootDirectory, ".test");
        if (!fs.Directory.Exists(outDir)) fs.Directory.CreateDirectory(outDir);

        switch (testing.Format)
        {
            case "junit":
                var junit = BuildJUnit(results);
                await fs.File.WriteAllTextAsync(fs.Path.Combine(outDir, "junit.xml"), junit);
                break;
            default: // "json"
                var json = BuildJson(results, testing);
                await fs.File.WriteAllTextAsync(fs.Path.Combine(outDir, "results.json"), json);
                break;
        }

        return App.Data.@this.Ok(results);
    }

    private static void RenderConsole(StringBuilder sb, App.Test.Results results, Test.@this testing)
    {
        var summary = results.Summary();
        var total = results.Count;
        sb.AppendLine($"Test summary: {total} total, "
            + $"{summary[TestStatus.Pass]} pass, {summary[TestStatus.Fail]} fail, "
            + $"{summary[TestStatus.Timeout]} timeout, {summary[TestStatus.Stale]} stale, "
            + $"{summary[TestStatus.Skipped]} skipped");

        foreach (var run in results)
        {
            var currentBuilderVersion = ResolveBuilderVersion(testing);
            var drift = !string.IsNullOrEmpty(run.File.BuilderVersion)
                && !string.IsNullOrEmpty(currentBuilderVersion)
                && !string.Equals(run.File.BuilderVersion, currentBuilderVersion, StringComparison.Ordinal);

            sb.AppendLine($"  [{run.Status}] {run.File.Path} ({run.Duration.TotalMilliseconds:F0}ms)"
                + (drift ? " [builder drift]" : ""));

            if (run.Status == TestStatus.Fail && run.Error != null)
                RenderFailure(sb, run);
        }
    }

    private static void RenderFailure(StringBuilder sb, TestRun run)
    {
        sb.AppendLine("    FAIL: " + run.File.Path);
        if (run.Error is AssertionError assert)
        {
            sb.AppendLine($"      Expected: {FormatValue(assert.Expected)}");
            sb.AppendLine($"      Actual:   {FormatValue(assert.Actual)}");
            if (assert.Variables is { Count: > 0 } variables)
            {
                sb.AppendLine("      Variables:");
                foreach (var (name, value) in variables)
                    sb.AppendLine($"        %{name}% = {FormatValue(value)}");
            }
        }
        else
        {
            sb.AppendLine($"      Error: {run.Error?.Message}");
        }
        if (!string.IsNullOrEmpty(run.CapturedOutput))
        {
            sb.AppendLine("      Output:");
            foreach (var line in StripAnsi(run.CapturedOutput).Split('\n'))
                sb.AppendLine("        " + line);
        }
    }

    private static void RenderCoverageTables(StringBuilder sb, Test.@this testing, Modules.@this modules)
    {
        sb.AppendLine();
        sb.AppendLine("Module.action coverage:");
        var observed = testing.Coverage.ModuleActions.ToHashSet();
        var universeCount = 0;
        var observedCount = observed.Count;
        foreach (var module in modules.Names.OrderBy(n => n))
        {
            foreach (var action in modules.GetActions(module).OrderBy(a => a))
            {
                universeCount++;
                var hit = observed.Contains((module, action)) ? "x" : " ";
                sb.AppendLine($"  [{hit}] {module}.{action}");
            }
        }
        sb.AppendLine($"  total: {observedCount}/{universeCount}");

        sb.AppendLine();
        sb.AppendLine("Branch coverage (condition.if):");
        if (testing.Coverage.Branches.Count == 0)
        {
            sb.AppendLine("  (no condition.if sites observed)");
        }
        else
        {
            var labelsMap = testing.Coverage.BranchLabels;
            foreach (var (site, indices) in testing.Coverage.Branches.OrderBy(kv => kv.Key))
            {
                // Prefer the human-readable labels ({if, elseif[1], else} or {true, false}).
                // Fall back to indices for any site that didn't get labels attached.
                string rendered;
                if (labelsMap.TryGetValue(site, out var labels) && labels.Count > 0)
                    rendered = string.Join(", ", labels.OrderBy(SortLabel));
                else
                    rendered = string.Join(", ", indices.OrderBy(i => i));
                sb.AppendLine($"  {site}: {{{rendered}}}");
            }
        }
    }

    // Order labels so "if" comes before "elseif[N]" before "else", and
    // "true" before "false". Alphabetical would scatter them.
    private static string SortLabel(string label) => label switch
    {
        "if" => "0",
        "true" => "0",
        "false" => "1",
        "else" => "Z",
        _ when label.StartsWith("elseif[") => "5" + label,
        _ => label
    };

    private static string BuildJson(App.Test.Results results, Test.@this testing)
    {
        var runs = new List<object>();
        foreach (var run in results)
        {
            runs.Add(new
            {
                path = run.File.Path,
                entryGoal = run.File.EntryGoalName,
                status = run.Status.ToString(),
                durationMs = run.Duration.TotalMilliseconds,
                goalHash = run.File.GoalHash,
                builderVersion = run.File.BuilderVersion,
                tags = run.File.Tags.Concat(run.UserTags).Distinct().ToList(),
                error = run.Error?.Message,
                expected = (run.Error as AssertionError)?.Expected,
                actual = (run.Error as AssertionError)?.Actual,
                variables = (run.Error as AssertionError)?.Variables
            });
        }
        var envelope = new
        {
            summary = results.Summary().ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            builderVersion = ResolveBuilderVersion(testing),
            runs
        };
        return JsonSerializer.Serialize(envelope, App.Utils.Json.CamelCaseIndented);
    }

    private static string BuildJUnit(App.Test.Results results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<testsuites tests=\"{results.Count}\" failures=\"{results.Count(r => r.Status == TestStatus.Fail)}\" errors=\"0\">");
        var byPath = results.GroupBy(r => System.IO.Path.GetDirectoryName(r.File.Path) ?? "");
        foreach (var group in byPath)
        {
            var suiteTests = group.ToList();
            var failures = suiteTests.Count(r => r.Status == TestStatus.Fail);
            var timeSec = suiteTests.Sum(r => r.Duration.TotalSeconds);
            sb.AppendLine($"  <testsuite name=\"{SecurityElement.Escape(group.Key)}\" tests=\"{suiteTests.Count}\" failures=\"{failures}\" time=\"{timeSec:F3}\">");
            foreach (var run in suiteTests)
            {
                var name = SecurityElement.Escape(run.File.Path) ?? "";
                sb.Append($"    <testcase name=\"{name}\" time=\"{run.Duration.TotalSeconds:F3}\"");
                if (run.Status == TestStatus.Pass) sb.AppendLine(" />");
                else
                {
                    sb.AppendLine(">");
                    switch (run.Status)
                    {
                        case TestStatus.Fail:
                            sb.AppendLine($"      <failure>{SecurityElement.Escape(run.Error?.Message ?? "fail")}</failure>");
                            break;
                        case TestStatus.Timeout:
                            sb.AppendLine($"      <failure type=\"timeout\">timeout</failure>");
                            break;
                        case TestStatus.Stale:
                        case TestStatus.Skipped:
                            sb.AppendLine($"      <skipped>{SecurityElement.Escape(run.File.StatusReason ?? run.Status.ToString())}</skipped>");
                            break;
                    }
                    sb.AppendLine("    </testcase>");
                }
            }
            sb.AppendLine("  </testsuite>");
        }
        sb.AppendLine("</testsuites>");
        return sb.ToString();
    }

    private static string? ResolveBuilderVersion(Test.@this testing) =>
        testing.App.Version;

    private static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        if (value is System.Collections.IEnumerable en and not string)
            try { return JsonSerializer.Serialize(value, App.Utils.Json.CamelCaseIndented); }
            catch { return value.ToString() ?? "(null)"; }
        return value.ToString() ?? "(null)";
    }

    // Strips ANSI escape sequences to prevent forged output in captured
    // test stdout (test writes bold-green "ok" via ANSI — rendered literally instead).
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static string StripAnsi(string input) => AnsiEscape.Replace(input, "");
}
