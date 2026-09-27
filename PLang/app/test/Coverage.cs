using System.Collections.Concurrent;

namespace app.test;

/// <summary>
/// Tracks what executed during a test run. Two dimensions:
///   - ModuleActions: which (module, action) handler pairs fired at least once
///   - Branches: per condition.if site, which branch indices were observed
/// Populated by the coverage subscriber on AfterAction; merged from each child App
/// into the parent App at test-end so the run-wide view unions all observations.
/// All mutations are thread-safe.
/// </summary>
public sealed class Coverage
{
    // Key format: "module.action". Value unused (set semantics via dictionary keys).
    private readonly ConcurrentDictionary<string, byte> _moduleActions = new(StringComparer.OrdinalIgnoreCase);

    // Key: site identifier ("goalName:stepIndex"). Value: set of observed branch indices.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _branches = new();

    /// <summary>Read-only view of observed (module, action) pairs.</summary>
    public IEnumerable<(string Module, string Action)> ModuleActions
    {
        get
        {
            foreach (var key in _moduleActions.Keys)
            {
                var dot = key.IndexOf('.');
                if (dot < 0) continue;
                yield return (key[..dot], key[(dot + 1)..]);
            }
        }
    }

    /// <summary>Read-only view of observed branch indices per site.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<int>> Branches =>
        _branches.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<int>)new HashSet<int>(kvp.Value.Keys));

    /// <summary>Records what runs in <paramref name="context"/>'s actor from now on: every action that starts
    /// (bound on the action type's <c>on.start.after</c>), and the branch a condition takes when it fires (its
    /// own result is truthy), keyed by its position in the step's action chain. Answers the binding; whoever
    /// watches removes it when the watch ends.</summary>
    public global::app.@event.binding.@this Watch(global::app.actor.context.@this context)
        => context.App.type.list["action"].Own().Bind("start", global::app.@event.When.after,
            async (item, result, ctx) =>
            {
                if (item is not global::app.goal.step.action.@this action) return ctx.Ok();
                RecordModuleAction(action.Module.Name, action.Name);
                if (action.IsCondition && await result.ToBooleanAsync())
                {
                    var site = Site(action.Step?.Goal, action.Step?.Index.ToString());
                    RecordBranch(site, action.Step != null ? action.Step.Code.IndexOf(action) : -1);
                    RecordBranchLabel(site, action.Name);
                }
                return ctx.Ok();
            },
            context.Actor, global::app.@event.binding.Scope.actor);

    // A condition step's site — "goal:stepIndex", the goal by its path.
    private string Site(global::app.goal.@this? goal, string? index)
        => $"{goal?.Path?.ToString() ?? goal?.Name ?? "?"}:{index ?? "?"}";

    /// <summary>Records that a handler fired. Idempotent — calling twice is a no-op.</summary>
    public void RecordModuleAction(string module, string actionName)
    {
        _moduleActions.TryAdd($"{module}.{actionName}", 0);
    }

    /// <summary>Records a branch index observed at the given condition.if site. Accumulates indices per site.</summary>
    public void RecordBranch(string site, int branchIndex)
    {
        var indices = _branches.GetOrAdd(site, _ => new ConcurrentDictionary<int, byte>());
        indices.TryAdd(branchIndex, 0);
    }

    // Parallel map: site → set of human-readable branch labels ({"if", "elseif[1]", "else"}
    // or {"true", "false"}). Populated alongside RecordBranch so the report can render
    // {if, else} instead of {0, 2}.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _branchLabels = new();

    public void RecordBranchLabel(string site, string label)
    {
        var labels = _branchLabels.GetOrAdd(site, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        labels.TryAdd(label, 0);
    }

    /// <summary>Read-only view of observed branch labels per site. Empty if only indices were recorded.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> BranchLabels =>
        _branchLabels.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)new HashSet<string>(kvp.Value.Keys));

    // Per-site declared chain, preserving author order. Seeded when a test is created
    // ahead of execution so truly-unreached sites still appear in the report, and
    // also populated at runtime when condition.if fires (first fire wins;
    // subsequent fires at the same site are a no-op).
    private readonly ConcurrentDictionary<string, List<string>> _branchChains = new();

    /// <summary>
    /// Records the declared branch chain for a site. Only stored the first time —
    /// seed-then-observe is safe; re-seeding with a different chain is ignored.
    /// </summary>
    public void RecordBranchChain(string site, IReadOnlyList<string> chain)
    {
        if (chain == null || chain.Count == 0) return;
        _branchChains.TryAdd(site, new List<string>(chain));
    }

    /// <summary>Seeds the declared branch chain of every condition step in <paramref name="goal"/>, so a
    /// site that never runs still shows in the report. One condition: {true, false}; several: their
    /// names in author order.</summary>
    public void Add(global::app.goal.@this goal)
    {
        foreach (var step in goal.Step.Items())
        {
            var conditions = step.Code.Items().Where(a => a.IsCondition).ToList();
            if (conditions.Count == 0) continue;
            RecordBranchChain(Site(goal, step.Index.ToString()), conditions.Count == 1
                ? new[] { "true", "false" }
                : conditions.Select(c => c.Name).ToArray());
        }
    }

    /// <summary>Read-only view of the declared chain per site (author order).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> BranchChains =>
        _branchChains.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value);

    /// <summary>
    /// The coverage as the console shows it: every module.action of <paramref name="modules"/> marked when
    /// it fired, then each condition site's branches (its declared chain, else what was observed) marked
    /// hit or missed, with the totals and the branches no test took.
    /// </summary>
    public string Text(global::app.module.list.@this modules)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Module.action coverage:");
        var observed = ModuleActions.ToHashSet();
        var universeCount = 0;
        foreach (var module in modules.Items().OrderBy(m => m.Name))
        {
            foreach (var action in module.ActionNames.OrderBy(a => a))
            {
                universeCount++;
                var hit = observed.Contains((module.Name, action)) ? "x" : " ";
                sb.AppendLine($"  [{hit}] {module.Name}.{action}");
            }
        }
        sb.AppendLine($"  total: {observed.Count}/{universeCount}");

        sb.AppendLine();
        sb.AppendLine("Branch coverage (condition.if):");

        var chains = BranchChains;
        var labelsMap = BranchLabels;
        var indicesMap = Branches;
        var allSites = new SortedSet<string>(chains.Keys.Concat(labelsMap.Keys).Concat(indicesMap.Keys), StringComparer.Ordinal);
        if (allSites.Count == 0)
        {
            sb.AppendLine("  (no condition.if sites observed)");
            return sb.ToString();
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
                    declared = observedLabels.OrderBy(Order).ToList();
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
                var hit = !labelBacked || observedLabels.Contains(branch);
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
        return sb.ToString();
    }

    // A branch label's place in a chain: "if" before "elseif[N]" before "else", "true" before "false" —
    // alphabetical would scatter them.
    private string Order(string label) => label switch
    {
        "if" => "0",
        "true" => "0",
        "false" => "1",
        "else" => "Z",
        _ when label.StartsWith("elseif[") => "5" + label,
        _ => label
    };

    /// <summary>Unions another Coverage's observations into this one. Called when a child App's coverage is merged into the parent after a test completes.</summary>
    public void Merge(Coverage other)
    {
        foreach (var key in other._moduleActions.Keys)
            _moduleActions.TryAdd(key, 0);
        foreach (var kvp in other._branches)
        {
            var indices = _branches.GetOrAdd(kvp.Key, _ => new ConcurrentDictionary<int, byte>());
            foreach (var idx in kvp.Value.Keys)
                indices.TryAdd(idx, 0);
        }
        foreach (var kvp in other._branchLabels)
        {
            var labels = _branchLabels.GetOrAdd(kvp.Key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            foreach (var label in kvp.Value.Keys)
                labels.TryAdd(label, 0);
        }
        foreach (var kvp in other._branchChains)
            _branchChains.TryAdd(kvp.Key, new List<string>(kvp.Value));
    }
}
