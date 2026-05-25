using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using app.errors;
using app.tester;
using app.variables;

namespace app.modules.test;

/// <summary>
/// Writes the run-wide Results to the console (always) and to a single file artefact
/// (either .test/results.json or .test/junit.xml, selected by Testing.Format).
/// Console output: summary line + per-test status, with a failure block that includes
/// Expected/Actual and the Variables snapshot captured on AssertionError.
/// Also emits two coverage tables: module.action universe vs observed, and per-site
/// branch-index coverage for condition.if. The .test/ directory lives at the app root
/// (users scope a run with Testing.Include/Exclude — output location stays predictable).
/// </summary>
[Action("report", Cacheable = false)]
public partial class report : IContext
{
    public partial data.@this<app.tester.Results>? Results { get; init; }
    public partial data.@this<string>? Format { get; init; }

    public async Task<data.@this> Run()
    {
        var results = Results?.Value ?? Context.App!.Tester.Results;
        var testing = Context.App!.Tester;
        var format = Format?.Value ?? testing.Format;

        var console = new StringBuilder();
        RenderConsole(console, results, testing);
        RenderCoverageTables(console, testing, Context.App.Modules);
        await Context.App.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, console.ToString());

        // Write the file artefact. .test/ lives at the app root per Q4 decision.
        var app = Context.App;
        var outDir = System.IO.Path.Combine(app.AbsolutePath, ".test");
        if (!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);

        string reportFile;
        string content;
        switch (format)
        {
            case "junit":
                content = BuildJUnit(results);
                reportFile = System.IO.Path.Combine(outDir, "junit.xml");
                await System.IO.File.WriteAllTextAsync(reportFile, content);
                break;
            default: // "json"
                content = BuildJson(results, testing);
                reportFile = System.IO.Path.Combine(outDir, "results.json");
                await System.IO.File.WriteAllTextAsync(reportFile, content);
                break;
        }

        // Surface the artefact for observability: PLang tests inspect these on
        // %report% (the write-to target) without a filesystem round-trip, which
        // hits goal-relative path resolution edge cases inside child Apps. All
        // values are primitives / scalars so assert.equals / assert.isTrue can
        // validate them unambiguously (assert.contains on long content strings
        // is sensitive to the builder LLM's Value/Container param ordering).
        var summary = results.Summary();
        int variableSnapshotCount = 0;
        foreach (var run in results)
        {
            if (run.Error is AssertionError ae && ae.Variables is { Count: > 0 })
                variableSnapshotCount++;
        }

        var result = global::app.data.@this.Ok(results);
        result.Properties.Set("format", format);
        result.Properties.Set("reportPath", reportFile);
        result.Properties.Set("content", content);
        result.Properties.Set("summaryTotal", results.Count);
        result.Properties.Set("summaryPass", summary[global::app.tester.Status.Pass]);
        result.Properties.Set("summaryFail", summary[global::app.tester.Status.Fail]);
        result.Properties.Set("variableSnapshotCount", variableSnapshotCount);
        return result;
    }

    private static void RenderConsole(StringBuilder sb, app.tester.Results results, app.tester.@this testing)
    {
        var summary = results.Summary();
        var total = results.Count;
        sb.AppendLine($"Test summary: {total} total, "
            + $"{summary[global::app.tester.Status.Pass]} pass, {summary[global::app.tester.Status.Fail]} fail, "
            + $"{summary[global::app.tester.Status.Timeout]} timeout, {summary[global::app.tester.Status.Stale]} stale, "
            + $"{summary[global::app.tester.Status.Skipped]} skipped");

        foreach (var run in results)
        {
            var currentBuilderVersion = ResolveBuilderVersion(testing);
            var drift = !string.IsNullOrEmpty(run.File.BuilderVersion)
                && !string.IsNullOrEmpty(currentBuilderVersion)
                && !string.Equals(run.File.BuilderVersion, currentBuilderVersion, StringComparison.Ordinal);

            sb.AppendLine($"  [{run.Status}] {run.File.Path} ({run.Duration.TotalMilliseconds:F0}ms)"
                + (drift ? " [builder drift]" : ""));

            if (run.Status == global::app.tester.Status.Fail && run.Error != null)
                RenderFailure(sb, run);
        }
    }

    private static void RenderFailure(StringBuilder sb, global::app.tester.Run run)
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
        if (!string.IsNullOrEmpty(run.Output))
        {
            sb.AppendLine("      Output:");
            foreach (var line in StripAnsi(run.Output).Split('\n'))
                sb.AppendLine("        " + line);
        }
    }

    private static void RenderCoverageTables(StringBuilder sb, app.tester.@this testing, global::app.modules.@this modules)
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

        var chains = testing.Coverage.BranchChains;
        var labelsMap = testing.Coverage.BranchLabels;
        var indicesMap = testing.Coverage.Branches;
        var allSites = new SortedSet<string>(
            chains.Keys.Concat(labelsMap.Keys).Concat(indicesMap.Keys),
            StringComparer.Ordinal);

        if (allSites.Count == 0)
        {
            sb.AppendLine("  (no condition.if sites observed)");
            return;
        }

        int sitesComplete = 0, sitesPartial = 0, sitesUnreached = 0;
        int declaredTotal = 0, hitTotal = 0;
        var untested = new List<(string Site, List<string> Missing)>();

        foreach (var site in allSites)
        {
            var declared = chains.TryGetValue(site, out var chain) ? chain : null;
            var observedLabels = labelsMap.TryGetValue(site, out var labels) ? labels : new HashSet<string>();

            // Fall back to observed-as-declared when no chain was recorded (safety net
            // for runtime paths that skip seeding, e.g. tests that call test.run directly).
            bool labelBacked = true;
            if (declared == null || declared.Count == 0)
            {
                if (observedLabels.Count > 0)
                    declared = observedLabels.OrderBy(SortLabel).ToList();
                else if (indicesMap.TryGetValue(site, out var indices))
                {
                    // Render raw indices so Coverage.RecordBranch-only callers still
                    // produce readable output — no declared chain means we treat every
                    // observed index as the full universe.
                    declared = indices.OrderBy(i => i).Select(i => i.ToString()).ToList();
                    labelBacked = false;
                }
                else declared = new List<string>();
            }

            var missing = new List<string>();
            var parts = new List<string>();
            foreach (var branch in declared)
            {
                // Without labels, anything in 'declared' is by construction observed.
                var hit = labelBacked ? observedLabels.Contains(branch) : true;
                parts.Add((hit ? "✅ " : "❌ ") + branch);
                declaredTotal++;
                if (hit) hitTotal++;
                else missing.Add(branch);
            }

            sb.AppendLine($"  {site}: {{{string.Join(", ", parts)}}}");

            if (missing.Count == 0) sitesComplete++;
            else if (observedLabels.Count == 0) { sitesUnreached++; untested.Add((site, missing)); }
            else { sitesPartial++; untested.Add((site, missing)); }
        }

        var percent = declaredTotal > 0 ? (int)Math.Round(100.0 * hitTotal / declaredTotal) : 0;
        sb.AppendLine();
        sb.AppendLine($"  Sites: {allSites.Count} total ({sitesComplete} complete, {sitesPartial} partial, {sitesUnreached} unreached)");
        sb.AppendLine($"  Branches: {hitTotal}/{declaredTotal} covered ({percent}%)");

        if (untested.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Untested branches:");
            foreach (var (site, missing) in untested)
                sb.AppendLine($"    {site}  {string.Join(", ", missing)}");
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

    private static string BuildJson(app.tester.Results results, app.tester.@this testing)
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
                variables = (run.Error as AssertionError)?.Variables,
                output = run.Output ?? "",
                timings = run.Timings.Select(t => new { stepIndex = t.StepIndex, ms = t.Ms }).ToList()
            });
        }
        // Structured coverage block — no emoji; downstream tooling renders as it likes.
        var branchCoverage = new Dictionary<string, object>();
        var labelsMap = testing.Coverage.BranchLabels;
        var chains = testing.Coverage.BranchChains;
        foreach (var site in chains.Keys.Concat(labelsMap.Keys).Distinct())
        {
            var declared = chains.TryGetValue(site, out var chain) ? (IReadOnlyList<string>)chain : Array.Empty<string>();
            var observed = labelsMap.TryGetValue(site, out var labels) ? labels.ToList() : new List<string>();
            branchCoverage[site] = new { declared, observed };
        }

        var envelope = new
        {
            summary = results.Summary().ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            builderVersion = ResolveBuilderVersion(testing),
            runs,
            branchCoverage
        };
        return JsonSerializer.Serialize(envelope, global::app.Diagnostics.Format.Options);
    }

    private static string BuildJUnit(app.tester.Results results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<testsuites tests=\"{results.Count}\" failures=\"{results.Count(r => r.Status == global::app.tester.Status.Fail)}\" errors=\"0\">");
        var byPath = results.GroupBy(r => System.IO.Path.GetDirectoryName(r.File.Path) ?? "");
        foreach (var group in byPath)
        {
            var suiteTests = group.ToList();
            var failures = suiteTests.Count(r => r.Status == global::app.tester.Status.Fail);
            var timeSec = suiteTests.Sum(r => r.Duration.TotalSeconds);
            sb.AppendLine($"  <testsuite name=\"{SecurityElement.Escape(group.Key)}\" tests=\"{suiteTests.Count}\" failures=\"{failures}\" time=\"{timeSec:F3}\">");
            foreach (var run in suiteTests)
            {
                var name = SecurityElement.Escape(run.File.Path) ?? "";
                sb.Append($"    <testcase name=\"{name}\" time=\"{run.Duration.TotalSeconds:F3}\"");
                if (run.Status == global::app.tester.Status.Pass) sb.AppendLine(" />");
                else
                {
                    sb.AppendLine(">");
                    switch (run.Status)
                    {
                        case global::app.tester.Status.Fail:
                            sb.AppendLine($"      <failure>{SecurityElement.Escape(run.Error?.Message ?? "fail")}</failure>");
                            break;
                        case global::app.tester.Status.Timeout:
                            sb.AppendLine($"      <failure type=\"timeout\">timeout</failure>");
                            break;
                        case global::app.tester.Status.Stale:
                        case global::app.tester.Status.Skipped:
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

    private static string? ResolveBuilderVersion(app.tester.@this testing) =>
        testing.App.Version;

    private static string FormatValue(object? value) => global::app.Diagnostics.Format.Value(value);

    // Strips ANSI escape sequences to prevent forged output in captured
    // test stdout (test writes bold-green "ok" via ANSI — rendered literally instead).
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static string StripAnsi(string input) => AnsiEscape.Replace(input, "");
}
