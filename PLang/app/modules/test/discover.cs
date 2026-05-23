using System.IO;
using System.Reflection;
using app.Attributes;
using app.errors;
using app.tester;
using app.Utils;
using app.variables;
using Goal = app.goals.goal.@this;

namespace app.modules.test;

/// <summary>
/// Walks a directory tree for *.test.goal files, loads each file's .pr, checks
/// freshness against the current .goal text (SHA-256 of Name + concat(Step.Text)),
/// extracts user tags (via test.tag actions) and auto-tags (via [RequiresCapability]
/// on the action handlers referenced in the .pr, recursing through static goal.call
/// chains), then applies the Testing.Include/Exclude tag filters. Filtered-out tests
/// are returned with Status=Skipped; tests with hash mismatch or missing .pr are
/// Status=Stale. Returns a List&lt;global::app.tester.File&gt; that test.run consumes.
/// </summary>
[ModuleDescription("Discover, run, and report on PLang test goals with tag filtering and coverage tracking")]
[System.ComponentModel.Description("Walk a directory for *.test.goal files and return a filtered list of global::app.tester.File descriptors")]
[Example("discover tests in 'Tests/Foo' recursive=false, write to %tests%",
    "test.discover Path([string] Tests/Foo), Recursive([bool] false) | variable.set Name([string] %tests%), Value([object] %!data%)")]
[Action("discover")]
public partial class discover : IContext
{
    /// <summary>Directory to walk. Resolved under the app root; traversal outside the root is rejected.</summary>
    [Default(".")]
    public partial data.@this<string> Path { get; init; }

    /// <summary>Filename pattern. Default matches PLang test convention.</summary>
    [Default("*.test.goal")]
    public partial data.@this<string> Pattern { get; init; }

    /// <summary>Walk subdirectories. Default true.</summary>
    [Default(true)]
    public partial data.@this<bool> Recursive { get; init; }

    public Task<data.@this> Run()
    {
        var app = Context.App!;

        global::app.data.@this empty = global::app.data.@this.Ok(new List<global::app.tester.File>());

        string absRoot;
        try { absRoot = global::app.types.path.file.@this.ValidatePath(Path.Value, app); }
        catch (ArgumentException)
        {
            // Empty/invalid path → return empty list, don't throw.
            return Task.FromResult(empty);
        }

        // Discovery is constrained to the app root — a traversal path
        // (../../etc) that ValidatePath normalised outside the root is
        // rejected, so a malicious --test config can't enumerate system
        // directories. ValidatePath no longer gates (Authorize does, for
        // file actions); discovery scopes itself explicitly.
        var rootPrefix = System.IO.Path.GetFullPath(app.AbsolutePath);
        if (!absRoot.StartsWith(rootPrefix, global::app.types.path.@this.RootComparison))
            return Task.FromResult(empty);

        if (!System.IO.Directory.Exists(absRoot))
            return Task.FromResult(empty);

        var option = Recursive.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var matches = System.IO.Directory.EnumerateFiles(absRoot, Pattern.Value, option);

        var include = Context.App.Tester.Include;
        var exclude = Context.App.Tester.Exclude;

        var files = new List<global::app.tester.File>();
        foreach (var match in matches)
            files.Add(DiscoverOne(match, app, include, exclude));

        return Task.FromResult(global::app.data.@this.Ok(files));
    }

    /// <summary>Discovers metadata for a single .test.goal file.</summary>
    private global::app.tester.File DiscoverOne(string absGoalPath, global::app.@this app,
        HashSet<string> include, HashSet<string> exclude)
    {
        var dir = System.IO.Path.GetDirectoryName(absGoalPath) ?? app.AbsolutePath;
        var relGoalPath = NormalizeRelative(System.IO.Path.GetRelativePath(app.AbsolutePath, absGoalPath));
        var fileName = System.IO.Path.GetFileName(absGoalPath);
        var prFileName = System.IO.Path.ChangeExtension(fileName, ".pr").ToLowerInvariant();
        var absPrPath = System.IO.Path.Combine(dir, ".build", prFileName);
        // PrPath is relative to the test's own directory (not the parent app root) so
        // the per-test child App — rooted at global::app.tester.File.Directory — can resolve it directly.
        var relPrPath = ".build/" + prFileName;

        var stub = new global::app.tester.File
        {
            Path = relGoalPath,
            Directory = dir,
            PrPath = relPrPath
        };

        if (!System.IO.File.Exists(absPrPath))
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "no .pr";
            return stub;
        }

        // Deserialize the stored .pr JSON → Goal. Uses the same TypeMapping pipeline
        // file.read uses for .pr files, so the stored Hash and BuilderVersion come
        // directly from the built artefact.
        Goal? prGoal;
        try
        {
            var prText = System.IO.File.ReadAllText(absPrPath);
            var (converted, err) = global::app.types.@this.TryConvertTo(prText, typeof(Goal));
            prGoal = converted as Goal;
            if (prGoal == null)
            {
                stub.Status = global::app.tester.Status.Stale;
                stub.StatusReason = err?.Message ?? "pr corrupt";
                return stub;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "pr corrupt: " + ex.Message;
            return stub;
        }

        // Re-parse the current .goal text → Goal (Ingi's preferred path: read file →
        // Goal object → compare .Hash). Parse is the canonical text-to-Goal converter.
        Goal? currentGoal;
        try
        {
            var goalText = System.IO.File.ReadAllText(absGoalPath);
            currentGoal = Goal.Parse(goalText, relGoalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stub.Status = global::app.tester.Status.Stale;
            stub.StatusReason = "goal read error: " + ex.Message;
            return stub;
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

        // Seed branch-coverage chains for every condition.if site in this test's goal
        // tree — lets the report surface sites that exist in source but no test ever
        // reaches. Uses the same recursion as auto-tag traversal.
        var chainVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SeedBranchChains(prGoal, Context.App!.Tester.Coverage, chainVisited);

        var file = new global::app.tester.File
        {
            Path = relGoalPath,
            Directory = dir,
            PrPath = relPrPath,
            Goal = prGoal,
            EntryGoalName = prGoal.Name,
            GoalHash = prGoal.Hash,
            BuilderVersion = prGoal.BuilderVersion
        };
        foreach (var tag in tags) file.Tags.Add(tag);

        // Filter: exclude wins over include (architect §5.6 / test-designer locked).
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

    /// <summary>
    /// Unions [RequiresCapability] capabilities from all handlers referenced in this goal,
    /// recursing through statically-resolvable goal.call chains. Cycles / dynamic call
    /// targets (%var%) are handled by the visited set and a depth cap.
    /// </summary>
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

            // Static goal.call: follow the chain to capture called-goal capabilities.
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

    /// <summary>
    /// For every condition.if site in the goal tree, seed its declared chain on
    /// the given Coverage. Only the first condition.if in each step is treated as
    /// a site (subsequent elseif conditions are inner-fires during orchestration,
    /// not standalone sites). Recurses static goal.call targets the same way
    /// auto-tag traversal does; dynamic %var% goal names are skipped.
    /// </summary>
    private void SeedBranchChains(Goal goal, app.tester.Coverage coverage, HashSet<string> visited, int depth = 0)
    {
        if (depth > 50) return;
        if (!visited.Add(goal.Name)) return;

        var goalId = goal.Path ?? goal.Name ?? "?";
        var seededSteps = new HashSet<int>();
        var subGoals = new List<Goal>();

        goal.ForEachAction((step, action) =>
        {
            // First condition.if in this step defines the site — seed once per step.
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

            // Recurse into static goal.call targets — their condition.ifs count too.
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
