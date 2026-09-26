namespace PLang.Tests.App.Decider;

// The builder's bootstrap: python decides, C# writes. tools/decider/bootstrap.py recorded, for every goal
// of the plang builder (os/system/builder/**), the two remote answers — the decider's (stage 1, 2) and
// gpt-5.4-nano's in formal, with its retry. Here the builder's own C# path takes them, exactly as a build
// does: goal.Parse the .goal, each step's Pick takes the decider's answers, the walk, goal.Step.Read the
// answer (every check, the handlers' Build, the freeze) and, for the steps it refused, the retry; then
// build.fold. The agreement: C# refuses on the first answer exactly the steps python refused, and none
// after the retry.
public class BootstrapTests
{
    internal static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    private static object? Raw(System.Text.Json.JsonElement e) => e.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Raw(p.Value)),
        System.Text.Json.JsonValueKind.Array => e.EnumerateArray().Select(Raw).ToList(),
        System.Text.Json.JsonValueKind.Number => e.GetDouble(),
        System.Text.Json.JsonValueKind.String => e.GetString(),
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        _ => null,
    };

    private static List<string> Popular() =>
        System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "os", "system", "builder", "llm", "decider.json")))
            .RootElement.GetProperty("popular").EnumerateArray().Select(p => p.GetString()!).ToList();

    /// <summary>The builder's .goal files, relative to os/ (as the goals' paths are written).</summary>
    internal static List<string> Files() =>
        System.IO.Directory.GetFiles(System.IO.Path.Combine(RepoRoot(), "os", "system", "builder"), "*.goal", System.IO.SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}.build{System.IO.Path.DirectorySeparatorChar}"))
            .Select(f => System.IO.Path.GetRelativePath(System.IO.Path.Combine(RepoRoot(), "os"), f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

    /// <summary>One .goal file built from its recorded answers, and what disagreed with python.</summary>
    internal static async Task<(global::app.goal.@this Goal, List<string> Differ)> Built(string rel, global::app.actor.context.@this context)
    {
        var differ = new List<string>();
        var text = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(RepoRoot(), "os", rel));
        var root = global::app.goal.@this.Parse(text, global::app.type.item.path.@this.Resolve("/" + rel, context), context)!;
        foreach (var goal in new[] { root }.Concat(root.Child))
        {
            var recorded = System.IO.Path.Combine(RepoRoot(), "tools", "decider", "out", "bootstrap", "os", rel, goal.Name + ".json");
            var answers = System.Text.Json.JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(recorded)).RootElement;
            if (goal.Step.CountRaw == 0) continue;
            foreach (var stage in new[] { "answer1", "answer2" })
            {
                var answer = Make.Dict((System.Collections.IDictionary)Raw(answers.GetProperty(stage))!, context);
                foreach (var step in goal.Step.Items()) await step.Pick.Take(answer, Popular(), context);
            }
            await goal.Step.Scope(context);

            var caught = answers.GetProperty("caught").EnumerateArray().Select(c => c.GetInt32()).ToHashSet();
            var first = await goal.Step.Read(answers.GetProperty("answer").GetString()!, context);
            var refused = first?.Details?["steps"]?.ToString() is { Length: > 0 } s
                ? s.Split(", ").Select(int.Parse).ToHashSet() : new HashSet<int>();
            if (!refused.SetEquals(caught))
                differ.Add($"{rel} {goal.Name}: C# refused [{string.Join(", ", refused)}] where python refused [{string.Join(", ", caught)}]: {first?.Message}");
            if (answers.GetProperty("retry").GetString() is { } retry && first != null
                && await goal.Step.Read(retry, context) is { } second)
                differ.Add($"{rel} {goal.Name}: after the retry C# still refuses: {second.Message}");
        }
        var folded = await new global::app.module.action.build.code.Default().Fold(
            new global::app.module.action.build.fold(context) { Goal = context.Ok<global::app.goal.@this>(root) });
        if (!folded.Success) differ.Add($"{rel}: fold: {folded.Error!.Message}");
        return (root, differ);
    }

    [Test]
    public async Task EveryBuilderStep_TakesItsCode_WhereAndOnlyWherePythonAccepted()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var differ = new List<string>();
        var files = Files();
        foreach (var rel in files)
            differ.AddRange((await Built(rel, os.User.Context)).Differ);

        await Assert.That(files.Count).IsEqualTo(7);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }
}
