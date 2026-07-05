using System.Text;
using System.Text.RegularExpressions;
using app.error;
using app.variable;

namespace app.module.test;

/// <summary>
/// Writes the run-wide tests to the console (always) and to a single file artefact
/// (either .test/results.json or .test/junit.xml, selected by Test.Format).
/// The json artefact is the tests' own wire form — each test serializes its [Out]
/// fields, no hand-mapped shape. Console output: summary line + per-test status,
/// with a failure block that includes Expected/Actual and the Variables snapshot
/// captured on AssertionError, plus two coverage tables (module.action universe vs
/// observed, and per-site branch coverage for condition.if). The .test/ directory
/// lives at the app root.
/// </summary>
[Action("report", Cacheable = false)]
public partial class report : IContext
{
    public partial data.@this<global::app.type.list.@this<global::app.test.@this>>? Results { get; init; }
    public partial data.@this<global::app.type.text.@this>? Format { get; init; }

    public async Task<data.@this> Run()
    {
        var testing = Context.App.Test;
        var results = await ResolveTests();
        var format = await ResolveFormat(testing);

        // Suppress the console summary when we're nested inside another test
        // (Current is set by test.run when it spins up the per-test child App).
        // The parent test consumes results via the returned Data Properties;
        // printing the nested run's status lines would pollute the outer output.
        if (testing.Current == null)
        {
            var console = new StringBuilder();
            RenderConsole(console, results, testing);
            RenderCoverageTables(console, testing, Context.App.Module);
            await Context.App.CurrentActor.Channel.WriteTextAsync(global::app.channel.list.@this.Output, console.ToString());
        }

        // Write the file artefact through path verbs (gated). .test/ lives at the app root.
        var context = Context;
        string content;
        global::app.type.path.@this writeTarget;
        if (format == global::app.test.Format.JUnit)
        {
            content = new global::app.test.junit.@this(results).ToString();
            writeTarget = global::app.type.path.@this.Resolve("/.test/junit.xml", context);
        }
        else
        {
            // The json artefact IS the tests' wire form — each test writes its [Out] fields.
            content = await Wire(results);
            writeTarget = global::app.type.path.@this.Resolve("/.test/results.json", context);
        }
        // WriteText creates parent dirs via EnsureParentDir; AuthGate(Write) gates it.
        var written = await writeTarget.WriteText(content);
        if (!written.Success) return Context.Error(written.Error!);

        // Surface the artefact for observability: PLang tests inspect these on %report%
        // without a filesystem round-trip. All values are scalars so assert.* validate them.
        var summary = global::app.test.list.@this.Summary(results);
        int variableSnapshotCount = results.Count(t => t.Error is AssertionError { Variables: { Count: > 0 } });

        // Return the tests so a parent runner can propagate them via `write to %results%`.
        var result = Context.Ok<global::app.type.list.@this<global::app.test.@this>>(
            new global::app.type.list.@this<global::app.test.@this>(results, Context));
        result.Properties.Set("format", format.ToString());
        result.Properties.Set("reportPath", writeTarget.Absolute);
        result.Properties.Set("content", content);
        result.Properties.Set("summaryTotal", results.Count);
        result.Properties.Set("summaryPass", summary[global::app.test.Status.Pass]);
        result.Properties.Set("summaryFail", summary[global::app.test.Status.Fail]);
        result.Properties.Set("variableSnapshotCount", variableSnapshotCount);
        return result;
    }

    // Resolve the tests to report: an explicit %results% list (each row Data-wrapped,
    // unwrap once at the boundary) or the session's accumulated tests.
    private async Task<IReadOnlyList<global::app.test.@this>> ResolveTests()
    {
        var passed = Results == null ? null : await Results.Value();
        if (passed == null) return Context.App.Test.Tests;
        var tests = new List<global::app.test.@this>();
        foreach (var row in passed)
            if (await row.Value() is global::app.test.@this t) tests.Add(t);
        return tests;
    }

    // The Format param overrides the session default when present.
    private async Task<global::app.test.Format> ResolveFormat(global::app.test.list.@this testing)
    {
        var over = Format == null ? null : (await Format.Value())?.Clr<string>();
        if (over == null) return testing.Format;
        return string.Equals(over, "junit", StringComparison.OrdinalIgnoreCase)
            ? global::app.test.Format.JUnit
            : global::app.test.Format.Json;
    }

    // The tests serialize themselves through the single wire serializer — each test's
    // [Out] fields become the artefact. No hand-built shape.
    private async Task<string> Wire(IReadOnlyList<global::app.test.@this> results)
    {
        var list = new global::app.type.list.@this<global::app.test.@this>(results, Context);
        var serializer = new global::app.channel.serializer.plang.@this(Context);
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, list, global::app.View.Out);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void RenderConsole(StringBuilder sb, IReadOnlyList<global::app.test.@this> results, global::app.test.list.@this testing)
    {
        var summary = global::app.test.list.@this.Summary(results);
        sb.AppendLine($"Test summary: {results.Count} total, "
            + $"{summary[global::app.test.Status.Pass]} pass, {summary[global::app.test.Status.Fail]} fail, "
            + $"{summary[global::app.test.Status.Timeout]} timeout, {summary[global::app.test.Status.Stale]} stale, "
            + $"{summary[global::app.test.Status.Skipped]} skipped");

        var currentBuilderVersion = testing.App.Version;
        foreach (var test in results)
        {
            var drift = !string.IsNullOrEmpty(test.Goal.BuilderVersion)
                && !string.IsNullOrEmpty(currentBuilderVersion)
                && !string.Equals(test.Goal.BuilderVersion, currentBuilderVersion, StringComparison.Ordinal);

            sb.AppendLine($"  [{test.Status}] {test.Goal.Path} ({test.Duration.TotalMilliseconds:F0}ms)"
                + (drift ? " [builder drift]" : ""));

            if (test.Status == global::app.test.Status.Fail && test.Error != null)
                RenderFailure(sb, test);
        }
    }

    private static void RenderFailure(StringBuilder sb, global::app.test.@this test)
    {
        sb.AppendLine("    FAIL: " + test.Goal.Path);
        if (test.Error is AssertionError assert)
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
            sb.AppendLine($"      Error: {test.Error?.Message}");
        }
        var output = test.Stdout?.Clr<string>();
        if (!string.IsNullOrEmpty(output))
        {
            sb.AppendLine("      Output:");
            foreach (var line in StripAnsi(output).Split('\n'))
                sb.AppendLine("        " + line);
        }
    }

    private static void RenderCoverageTables(StringBuilder sb, global::app.test.list.@this testing, global::app.module.@this modules)
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

            bool labelBacked = true;
            if (declared == null || declared.Count == 0)
            {
                if (observedLabels.Count > 0)
                    declared = observedLabels.OrderBy(SortLabel).ToList();
                else if (indicesMap.TryGetValue(site, out var indices))
                {
                    declared = indices.OrderBy(i => i).Select(i => i.ToString()).ToList();
                    labelBacked = false;
                }
                else declared = new List<string>();
            }

            var missing = new List<string>();
            var parts = new List<string>();
            foreach (var branch in declared)
            {
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

    private static string FormatValue(object? value) => global::app.Diagnostics.Format.Value(value);

    // Strips ANSI escape sequences to prevent forged output in captured
    // test stdout (test writes bold-green "ok" via ANSI — rendered literally instead).
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static string StripAnsi(string input) => AnsiEscape.Replace(input, "");
}
