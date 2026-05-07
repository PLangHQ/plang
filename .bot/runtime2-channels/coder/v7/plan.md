# v7 Plan — codeanalyzer v3 fixes

## Scope
Fix the two real findings in `PLang/App/Channels/Channel/Events/this.cs`. Defer I1.

## Changes

### 1. `_active` becomes instance state (B1)
```csharp
// Was: private static readonly AsyncLocal<HashSet<string>?> _active = new();
private readonly AsyncLocal<HashSet<string>?> _active = new();
```
Single keyword removal. Restores per-channel semantics.

### 2. `Enter` becomes copy-on-write (L1)
```csharp
public IDisposable Enter(string bindingId)
{
    var parent = _active.Value;
    var set = parent == null
        ? new HashSet<string> { bindingId }
        : new HashSet<string>(parent) { bindingId };
    _active.Value = set;
    return new Releaser(this, parent);
}

private sealed class Releaser : IDisposable
{
    private readonly @this _owner;
    private readonly HashSet<string>? _parent;
    public Releaser(@this owner, HashSet<string>? parent) { _owner = owner; _parent = parent; }
    public void Dispose() => _owner._active.Value = _parent;
}
```
Each `Enter` allocates its own set; child tasks inherit the parent reference (read-only) but any nested `Enter` makes a fresh copy. `Dispose` restores the parent reference, never mutates the inherited set.

## Tests
Existing tests cover the recursion-guard contract. No new tests added — the fixes preserve the same observable single-flow behaviour; the failure modes they prevent (cross-channel binding collision; parallel child fan-out from inside `Enter`) have no current callsite to exercise.

## Verification
- C# baseline: 2760/2760.
- C# after: 2760/2760.
- PLang: same pass set as v6 baseline (`_fixtures_fail/*`, `_fixtures_sensitive/*` are deliberately-failing test inputs, not regressions).

## Deferred
- **I1** (`Variables.Snapshot()` ignores overlay) — only consumed by Stage-9 migration stub; correct fix depends on whether snapshot semantics should reflect current visible scope or actor-shared only. Pick that up when a real caller lands.
