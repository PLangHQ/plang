using System.Reflection;
using app.Attributes;
using app.errors;
using app.tester;
using app.Utils;
using app.variables;
using Goal = app.goals.goal.@this;
using FilePath = app.types.path.file.@this;

namespace app.modules.test;

/// <summary>
/// Walks a directory tree for *.test.goal files (via <c>rootPath.List</c>, which
/// routes through <see cref="app.types.path.file.@this.AuthGate"/>), loads each
/// file's .pr through path verbs, checks freshness against the current .goal
/// text (SHA-256 of Name + concat(Step.Text)), extracts user tags (via
/// test.tag actions) and auto-tags (via [RequiresCapability] on the action
/// handlers referenced in the .pr, recursing through static goal.call chains),
/// then applies the Testing.Include/Exclude tag filters. Returns a
/// List&lt;global::app.tester.File&gt; that test.run consumes.
///
/// <para>The pre-AuthGate scan that this handler used to do —
/// <c>StartsWith(rootPrefix)</c> hand-rolled containment + raw
/// <c>System.IO.Directory.EnumerateFiles</c> — is gone. AuthGate is the only
/// gate, and an out-of-root <c>--test</c> path now surfaces as a permission
/// prompt or denial instead of a silent empty list.</para>
/// </summary>
[Action("discover")]
public partial class discover : IContext
{
    /// <summary>Directory to walk. AuthGate(Read) enforces in-root vs prompt-or-deny.</summary>
    [Default(".")]
    public partial data.@this<global::app.types.path.@this> Path { get; init; }

    /// <summary>Filename pattern. Default matches PLang test convention.</summary>
    [Default("*.test.goal")]
    public partial data.@this<string> Pattern { get; init; }

    /// <summary>Walk subdirectories. Default true.</summary>
    [Default(true)]
    public partial data.@this<bool> Recursive { get; init; }

    public async Task<data.@this> Run()
    {
        var app = Context.App!;
        global::app.data.@this empty = global::app.data.@this.Ok(new List<global::app.tester.File>());

        var root = Path.Value;
        if (root == null) return empty;

        // List routes through AuthGate(Read). Out-of-root: prompt or denial.
        var listed = await root.List(Pattern.Value!, Recursive.Value);
        if (!listed.Success) return global::app.data.@this.FromError(listed.Error!);
        if (listed.Value == null) return empty;

        var include = Context.App.Tester.Include;
        var exclude = Context.App.Tester.Exclude;

        var files = new List<global::app.tester.File>();
        foreach (var match in listed.Value)
        {
            // .test.goal files only resolve under the file scheme; foreign schemes
            // skip silently. The List call already returned filesystem paths.
            if (match is not FilePath fileMatch) continue;
            files.Add(await DiscoverOne(fileMatch, app, include, exclude));
        }
        return global::app.data.@this.Ok(files);
    }

    /// <summary>Discovers metadata for a single .test.goal file (FilePath form).</summary>
    private async Task<global::app.tester.File> DiscoverOne(FilePath goalFile, global::app.@this app,
        HashSet<string> include, HashSet<string> exclude)
    {
        var relGoalPath = NormalizeRelative(goalFile.Relative);
        var directory = goalFile.Parent?.Absolute ?? app.AbsolutePath;

        // PrPath sibling: <dir>/.build/<stem>.pr — derived via the generic verbs.
        var stem = goalFile.FileNameWithoutExtension.ToLowerInvariant();
        var prFile = goalFile.Parent?.Combine(".build").Combine(stem + ".pr") as FilePath
                     ?? (FilePath)goalFile.Combine(".build").Combine(stem + ".pr");
        // PrPath as PLang program sees it (relative to the test's own directory).
        var relPrPath = ".build/" + stem + ".pr";

        var stub = new global::app.tester.File
        {
            Path = relGoalPath,
            Directory = directory,
            PrPath = relPrPath
        };

        var prExists = await prFile.ExistsAsync();
        if (!prExists.Success || prExists.Value != true)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "no .pr";
            return stub;
        }

        // Read the .pr through the gated verb. MIME maps .pr → Goal via
        // ReadText's TryConvertTo branch.
        Goal? prGoal;
        var prRead = await prFile.ReadText();
        if (!prRead.Success)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = prRead.Error?.Message ?? "pr corrupt";
            return stub;
        }
        prGoal = prRead.Value as Goal;
        if (prGoal == null)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "pr corrupt";
            return stub;
        }

        // .goal source read — same gated verb. .goal MIME maps to Goal via Parse.
        Goal? currentGoal;
        var goalRead = await goalFile.ReadText();
        if (!goalRead.Success)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "goal read error: " + (goalRead.Error?.Message ?? "");
            return stub;
        }
        currentGoal = goalRead.Value as Goal;
        if (currentGoal == null)
        {
            // ReadText fell back to plain string — parse explicitly.
            var goalText = goalRead.Value as string ?? "";
            currentGoal = Goal.Parse(goalText, goalFile);
        }

        if (currentGoal == null || !string.Equals(currentGoal.Hash, prGoal.Hash, StringComparison.OrdinalIgnoreCase))
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "rebuild needed";
            return stub;
        }

        // Tags: user-declared (test.tag actions) + auto (handler [RequiresCapability]).
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractUserTags(prGoal, tags);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractAutoTags(prGoal, tags, visited);

        // Seed branch-coverage chains.
        var chainVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SeedBranchChains(prGoal, Context.App!.Tester.Coverage, chainVisited);

        var file = new global::app.tester.File
        {
            Path = relGoalPath,
            Directory = directory,
            PrPath = relPrPath,
            Goal = prGoal,
            EntryGoalName = prGoal.Name,
            GoalHash = prGoal.Hash,
            BuilderVersion = prGoal.BuilderVersion
        };
        foreach (var tag in tags) file.Tags.Add(tag);

        // Filter: exclude wins over include.
        if (exclude.Count > 0 && exclude.Overlaps(file.Tags))
        {
            file.Status = global::app.tester.Status.Skipped;
            file.StatusReason = "excluded by tag";
        }
        else if (include.Count > 0 && !include.Overlaps(file.Tags))
        {
            file.Status = global::app.tester.Status.Skipped;
            file.StatusReason = "no include match";
        }
        else
        {
            file.Status = global::app.tester.Status.Ready;
        }
        return file;
    }

    private static void ExtractUserTags(Goal goal, HashSet<string> tags)
    {
        goal.ForEachAction((step, action) =>
        {
            if (!string.Equals(action.Module, "test", StringComparison.OrdinalIgnoreCase)) return;
            if (!string.Equals(action.ActionName, "tag", StringComparison.OrdinalIgnoreCase)) return;
            var tagsParam = action.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, "Tags", StringComparison.OrdinalIgnoreCase));
            if (tagsParam?.Value is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    var s = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) tags.Add(s);
                }
            }
            else if (tagsParam?.Value is string single && !string.IsNullOrWhiteSpace(single))
            {
                tags.Add(single);
            }
        });
    }

    private void ExtractAutoTags(Goal goal, HashSet<string> tags, HashSet<string> visited, int depth = 0)
    {
        if (depth > 50) return;
        if (!visited.Add(goal.Name)) return;

        var modules = Context.App!.Modules;
        var subGoals = new List<Goal>();
        goal.ForEachAction((step, action) =>
        {
            var type = modules.GetActionType(action.Module, action.ActionName);
            var attr = type?.GetCustomAttribute<RequiresCapabilityAttribute>();
            if (attr != null)
                foreach (var cap in attr.Capabilities)
                    tags.Add(cap);

            if (string.Equals(action.Module, "goal", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action.ActionName, "call", StringComparison.OrdinalIgnoreCase))
            {
                var targetName = ResolveStaticGoalName(action);
                if (targetName != null)
                {
                    var sub = Context.App.Goals.Get(targetName);
                    if (sub != null) subGoals.Add(sub);
                }
            }
        });
        foreach (var sub in subGoals)
            ExtractAutoTags(sub, tags, visited, depth + 1);
    }

    private static string? ResolveStaticGoalName(app.goals.goal.steps.step.actions.action.@this action)
    {
        var nameParam = action.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "GoalName", StringComparison.OrdinalIgnoreCase));
        var value = nameParam?.Value;
        var name = value switch
        {
            GoalCall gc => gc.Name,
            string s => s,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Object
                && je.TryGetProperty("Name", out var np) => np.GetString(),
            System.Collections.Generic.IDictionary<string, object?> dict when dict.TryGetValue("Name", out var nm) => nm?.ToString(),
            _ => null
        };
        if (string.IsNullOrEmpty(name) || name.Contains('%')) return null;
        return name;
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/');

    private void SeedBranchChains(Goal goal, app.tester.Coverage coverage, HashSet<string> visited, int depth = 0)
    {
        if (depth > 50) return;
        if (!visited.Add(goal.Name)) return;

        var goalId = goal.Path?.ToString() ?? goal.Name ?? "?";
        var seededSteps = new HashSet<int>();
        var subGoals = new List<Goal>();

        goal.ForEachAction((step, action) =>
        {
            if (seededSteps.Add(step.Index))
            {
                int firstIfIndex = step.Actions.FirstConditionIndex();
                if (firstIfIndex >= 0)
                {
                    var chain = step.Actions.ComputeBranchChain(firstIfIndex);
                    var site = $"{goalId}:{step.Index}";
                    coverage.RecordBranchChain(site, chain);
                }
            }

            if (string.Equals(action.Module, "goal", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action.ActionName, "call", StringComparison.OrdinalIgnoreCase))
            {
                var targetName = ResolveStaticGoalName(action);
                if (targetName != null)
                {
                    var sub = Context.App!.Goals.Get(targetName);
                    if (sub != null) subGoals.Add(sub);
                }
            }
        });

        foreach (var sub in subGoals)
            SeedBranchChains(sub, coverage, visited, depth + 1);
    }
}
