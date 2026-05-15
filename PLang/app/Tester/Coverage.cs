using System.Collections.Concurrent;

namespace app.Tester;

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

    // Per-site declared chain, preserving author order. Seeded by test.discover
    // ahead of execution so truly-unreached sites still appear in the report, and
    // also populated at runtime when condition.if fires (first fire wins;
    // subsequent fires at the same site are a no-op).
    private readonly ConcurrentDictionary<string, List<string>> _branchChains = new();

    /// <summary>
    /// Records the declared branch chain for a site. Only stored the first time —
    /// seed-then-observe is safe; re-seeding with a different chain is ignored.
    /// </summary>
    public void RecordBranchChain(string site, List<string> chain)
    {
        if (chain == null || chain.Count == 0) return;
        _branchChains.TryAdd(site, new List<string>(chain));
    }

    /// <summary>Read-only view of the declared chain per site (author order).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> BranchChains =>
        _branchChains.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value);

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
