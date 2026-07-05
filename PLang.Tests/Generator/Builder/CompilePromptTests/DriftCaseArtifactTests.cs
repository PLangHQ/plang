using System.Text.Json;

namespace PLang.Tests.Build.CompilePromptTests;

/// <summary>
/// Architect's load-bearing verification (per
/// .bot/compile-llm-notes-per-action/architect/plan.md "Verification" section)
/// asserts the committed Tests/Simple/.build/start.pr is in the post-fix shape:
///
///   - `write out %message%` (no channel clause) → output.write with Data only;
///     no `channel` parameter, no `channel=%!data%` token in formal.
///   - `assert %message% equals 'hello plang'` (no custom message) →
///     assert.equals with Expected="hello plang", Actual=%message%; no Message
///     parameter; formal (if present) has no Message= token.
///
/// The plang-test sibling at Tests/Builder/CompileLlmNotes/ documents the same
/// rule but the runtime substring-asserts on .pr (`application/plang-goal` MIME)
/// require a Goal-object container, not a string haystack — these C# tests are
/// the load-bearing structural check.
///
/// Architect's 3-fresh-cache rule (delete Tests/Simple/.build, rebuild, run, x3)
/// is the *real* drift verification. These tests guard the committed artifact
/// after each build round so a regression of the post-fix shape goes red in CI.
/// </summary>
public class DriftCaseArtifactTests
{
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string StartPrPath =
        Path.Combine(RepoRoot, "Tests", "Simple", ".build", "start.test.pr");

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Tests", "Simple", ".build")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) throw new InvalidOperationException("Tests/Simple/.build not found");
        return dir;
    }

    private static JsonElement? FindStepByTextPrefix(JsonDocument doc, string prefix)
    {
        var steps = doc.RootElement.GetProperty("steps");
        foreach (var step in steps.EnumerateArray())
        {
            var text = step.GetProperty("text").GetString() ?? "";
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return step;
        }
        return null;
    }

    private static IEnumerable<string> ParamNames(JsonElement step, string module, string action)
    {
        foreach (var act in step.GetProperty("actions").EnumerateArray())
        {
            if (act.GetProperty("module").GetString() != module) continue;
            if (act.GetProperty("action").GetString() != action) continue;
            foreach (var p in act.GetProperty("parameters").EnumerateArray())
                yield return p.GetProperty("name").GetString() ?? "";
        }
    }

    [Test]
    public async Task DriftCase1_OutputWriteHasNoSpuriousChannelParameter()
    {
        await using var fs = File.OpenRead(StartPrPath);
        using var doc = await JsonDocument.ParseAsync(fs);

        var step = FindStepByTextPrefix(doc, "write out ");
        await Assert.That(step).IsNotNull();

        var names = ParamNames(step!.Value, "output", "write").ToList();
        await Assert.That(names).DoesNotContain("channel");
        // Data IS expected — sanity that we're looking at the right action.
        await Assert.That(names).Contains("Data");
    }

    [Test]
    public async Task DriftCase2_AssertEqualsOmitsMessageWhenStepTextHasNone()
    {
        await using var fs = File.OpenRead(StartPrPath);
        using var doc = await JsonDocument.ParseAsync(fs);

        var step = FindStepByTextPrefix(doc, "assert ");
        await Assert.That(step).IsNotNull();

        var names = ParamNames(step!.Value, "assert", "equals").ToList();
        await Assert.That(names).DoesNotContain("Message");
        // Expected + Actual ARE expected — pin them to confirm step shape.
        await Assert.That(names).Contains("Expected");
        await Assert.That(names).Contains("Actual");
    }

    [Test]
    public async Task DriftCase2_AssertEqualsExpectedMatchesStepTextLiteral()
    {
        await using var fs = File.OpenRead(StartPrPath);
        using var doc = await JsonDocument.ParseAsync(fs);

        var step = FindStepByTextPrefix(doc, "assert ");
        await Assert.That(step).IsNotNull();

        string? expectedValue = null;
        foreach (var act in step!.Value.GetProperty("actions").EnumerateArray())
        {
            if (act.GetProperty("module").GetString() != "assert") continue;
            if (act.GetProperty("action").GetString() != "equals") continue;
            foreach (var p in act.GetProperty("parameters").EnumerateArray())
            {
                if (p.GetProperty("name").GetString() == "Expected")
                {
                    expectedValue = p.GetProperty("value").GetString();
                }
            }
        }
        // Step text literal is 'hello plang' — drift case had this go to %!data%.
        await Assert.That(expectedValue).IsEqualTo("hello plang");
    }
}
