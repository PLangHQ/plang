using System.Reflection;
using app.Attributes;
using app.error;
using app.tester;
using app.Utils;
using app.variable;
using Goal = app.goal.@this;
using FilePath = app.type.path.file.@this;

namespace app.module.test;

/// <summary>
/// Walks a directory tree for *.test.goal files (via <c>rootPath.List</c>, which
/// routes through <see cref="app.type.path.file.@this.AuthGate"/>), loads each
/// file's .pr through path verbs, checks freshness against the current .goal
/// text (SHA-256 of Name + concat(Step.Text)), extracts user tags (via
/// test.tag actions) and auto-tags (via [RequiresCapability] on the action
/// handlers referenced in the .pr, recursing through static goal.call chains),
/// then applies the Testing.Include/Exclude tag filters. Returns a
/// List&lt;global::app.tester.test.@this&gt; that test.run consumes.
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
    public partial data.@this<global::app.type.path.@this> Path { get; init; }

    /// <summary>Filename pattern. Default matches PLang test convention.</summary>
    [Default("*.test.goal")]
    public partial data.@this<global::app.type.text.@this> Pattern { get; init; }

    /// <summary>Walk subdirectories. Default true.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<global::app.type.list.@this<global::app.tester.test.@this>>> Run()
    {
        var app = Context.App!;
        var empty = data.@this<global::app.type.list.@this<global::app.tester.test.@this>>.Ok(new global::app.type.list.@this<global::app.tester.test.@this>());

        var root = await Path.Value();
        if (root == null) return empty;

        // List routes through AuthGate(Read). Out-of-root: prompt or denial.
        var listed = await root.List((await Pattern.Value())!.Clr<string>()!, (await Recursive.Value())!.Value);
        if (!listed.Success) return data.@this<global::app.type.list.@this<global::app.tester.test.@this>>.FromError(listed.Error!);
        if (await listed.Value() == null) return empty;

        var include = Context.App.Tester.Include;
        var exclude = Context.App.Tester.Exclude;

        var files = new List<global::app.tester.test.@this>();
        var matches = global::app.type.item.@this.Lower<List<global::app.type.path.@this>>(await listed.Value())!;
        foreach (var match in matches)
        {
            // .test.goal files only resolve under the file scheme; foreign schemes
            // skip silently. The List call already returned filesystem paths.
            if (match is not FilePath fileMatch) continue;
            files.Add(await DiscoverOne(fileMatch, app, include, exclude));
        }
        return data.@this<global::app.type.list.@this<global::app.tester.test.@this>>.Ok(global::app.type.list.@this<global::app.tester.test.@this>.Of(files));
    }

    /// <summary>Discovers metadata for a single .test.goal file (FilePath form).</summary>
    private async Task<global::app.tester.test.@this> DiscoverOne(FilePath goalFile, global::app.@this app,
        HashSet<string> include, HashSet<string> exclude)
    {
        // Read the .goal source first — even when the .pr is missing or
        // corrupt, the source goal is enough to identify the file.
        // ReadText returns a typed Goal when the file MIME is application/
        // plang-goal, or a string fallback that we explicitly parse.
        var goalRead = await goalFile.ReadText();
        if (!goalRead.Success)
        {
            // Build a minimal goal from just the file's path so Test.Goal
            // is never null. Status=Stale with the read error as reason.
            return new global::app.tester.test.@this
            {
                Goal = new Goal { Path = goalFile },
                Status = global::app.tester.Status.Stale,
                StatusReason = "goal read error: " + (goalRead.Error?.Message ?? "")
            };
        }
        // Born-typed: text content rides as the text wrapper; its string form
        // is ToString (a Goal already matched the first arm).
        var sourceGoal = (await goalRead.Value()) as Goal
            ?? Goal.Parse((await goalRead.Value())?.ToString() ?? "", goalFile)
            ?? new Goal { Path = goalFile };

        // A goal whose source has a `tag this test 'skip'` step is PARKED: it registers
        // Skipped straight from the source text — before any build/freshness/.pr check — so
        // a deferred but REAL test reads honestly as Skipped, never as a no-op pass and never
        // as a stale failure. The tag step needn't be built or run. Re-enable by removing it.
        if (HasSkipTag(sourceGoal))
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Skipped,
                StatusReason = "tagged 'skip'"
            };

        // PrPath is derived on the goal from its Path. The corresponding
        // build artefact may or may not exist.
        var prFile = sourceGoal.PrPath as FilePath;

        if (prFile == null)
        {
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Stale,
                StatusReason = "no PrPath derivable from goal source"
            };
        }

        var prExists = await prFile.ExistsAsync();
        if (!prExists.Success || (await prExists.Value())?.Value != true)
        {
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Stale,
                StatusReason = "no .pr"
            };
        }

        // Read the .pr through the gated verb. MIME maps .pr → Goal via
        // ReadText's TryConvert branch.
        var prRead = await prFile.ReadText();
        if (!prRead.Success)
        {
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Stale,
                StatusReason = prRead.Error?.Message ?? "pr corrupt"
            };
        }
        var prGoal = (await prRead.Value()) as Goal;
        if (prGoal == null)
        {
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Stale,
                StatusReason = "pr corrupt"
            };
        }

        if (!string.Equals(sourceGoal.Hash, prGoal.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return new global::app.tester.test.@this
            {
                Goal = sourceGoal,
                Status = global::app.tester.Status.Stale,
                StatusReason = "rebuild needed"
            };
        }

        // Tags: user-declared (test.tag actions) + auto (handler [RequiresCapability]).
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractUserTags(prGoal, tags);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractAutoTags(prGoal, tags, visited);

        // Seed branch-coverage chains.
        var chainVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SeedBranchChains(prGoal, Context.App!.Tester.Coverage, chainVisited);

        var file = new global::app.tester.test.@this
        {
            Goal = prGoal,
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

    // The `skip` tag, read from the SOURCE step text (not built actions) so it works
    // before/without a build. Matches `tag this test 'skip'` (any quoting/casing/spacing).
    private static readonly System.Text.RegularExpressions.Regex SkipTagRegex = new(
        @"^\s*tag\s+this\s+test\s+['""]skip['""]\s*$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool HasSkipTag(Goal goal)
    {
        foreach (var step in goal.Steps)
            if (IsSkipTagStep(step.Text)) return true;
        return false;
    }

    /// <summary>
    /// True when a step's source text is exactly the skip directive
    /// <c>tag this test 'skip'</c> (any quoting / casing / spacing). This is the gate
    /// between an honest Skipped and a run, so it matches only the literal <c>skip</c> tag —
    /// a different tag value, or trailing args, does not match. Exposed for tests to pin the
    /// boundary.
    /// </summary>
    public static bool IsSkipTagStep(string text) => SkipTagRegex.IsMatch(text);

    private static void ExtractUserTags(Goal goal, HashSet<string> tags)
    {
        goal.ForEachAction((step, action) =>
        {
            if (!string.Equals(action.Module, "test", StringComparison.OrdinalIgnoreCase)) return;
            if (!string.Equals(action.ActionName, "tag", StringComparison.OrdinalIgnoreCase)) return;
            var tagsParam = action.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, "Tags", StringComparison.OrdinalIgnoreCase));
            if (tagsParam?.Peek() is app.type.list.@this nativeList)
            {
                // The Tags param is the native list value type — read each element's value.
                foreach (var item in nativeList.Items)
                {
                    var s = item.Peek()?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) tags.Add(s);
                }
            }
            else if (tagsParam?.Peek() is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    var s = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) tags.Add(s);
                }
            }
            else if (tagsParam?.Peek() is global::app.type.text.@this single && single.IsTruthy())
            {
                tags.Add(single.Clr<string>()!);
            }
        });
    }

    private void ExtractAutoTags(Goal goal, HashSet<string> tags, HashSet<string> visited, int depth = 0)
    {
        if (depth > 50) return;
        if (!visited.Add(goal.Name)) return;

        var modules = Context.App!.Module;
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
                    var sub = Context.App.Goal.Get(targetName);
                    if (sub != null) subGoals.Add(sub);
                }
            }
        });
        foreach (var sub in subGoals)
            ExtractAutoTags(sub, tags, visited, depth + 1);
    }

    private static string? ResolveStaticGoalName(app.goal.steps.step.actions.action.@this action)
    {
        var nameParam = action.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "GoalName", StringComparison.OrdinalIgnoreCase));
        var value = nameParam?.Peek();
        var name = value switch
        {
            GoalCall gc => gc.Name,
            global::app.type.text.@this s => s.Clr<string>(),
            global::app.type.item.clr { Value: System.Text.Json.JsonElement je }
                when je.ValueKind == System.Text.Json.JsonValueKind.Object
                && je.TryGetProperty("Name", out var np) => np.GetString(),
            // A goal.call param read back from the .pr is the native dict value type.
            app.type.dict.@this nd when nd.Get("Name") is { } nameData => nameData.Peek()?.ToString(),
            global::app.type.item.clr { Value: System.Collections.Generic.IDictionary<string, object?> dict }
                when dict.TryGetValue("Name", out var nm) => nm?.ToString(),
            _ => null
        };
        if (string.IsNullOrEmpty(name) || name.Contains('%')) return null;
        return name;
    }

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
                    var sub = Context.App!.Goal.Get(targetName);
                    if (sub != null) subGoals.Add(sub);
                }
            }
        });

        foreach (var sub in subGoals)
            SeedBranchChains(sub, coverage, visited, depth + 1);
    }
}
