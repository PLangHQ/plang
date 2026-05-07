# Coder summary — runtime2-channels

## Version

**v7** (current). v1–v6 history kept in this branch's report.json.

## What this is
The `Channel.Events.@this` type owns each channel's event-binding list, lock, recursion guard, and per-channel filter. Introduced in v6 to encapsulate state that previously leaked across `Channel.@this` and binding-firing helpers (OBP: the type that owns the data also owns its access rules — same spirit as `App.Goals.Goal.Events`).

Codeanalyzer v3 found two flaws in that v6 introduction:
- the recursion-guard `AsyncLocal` was declared `static`, so it became per-flow across all channels instead of per-channel;
- the guard's `HashSet<string>` is mutable and inherited by spawned children — any future parallel fan-out from inside a binding handler would let child tasks corrupt the parent's active-set.

v7 fixes both. A third finding (I1: `Variables.Snapshot()` ignores the overlay) was deferred — only the Stage-9 migration stub calls it today, and the correct semantics are a design call.

## What was done
- **B1 fix:** dropped `static` from `_active` in `PLang/App/Channels/Channel/Events/this.cs:22`. Now per-instance.
- **L1 fix:** `Enter(...)` is copy-on-write — snapshot parent set into a fresh HashSet, install it, and `Releaser.Dispose` restores the parent reference. `Releaser` no longer mutates a shared set.

Tests:
- C# baseline & after: 2760/2760 pass.
- PLang: 205 pass; 6 `_fixtures_fail/*` and `_fixtures_sensitive/*.fixture.goal` are intentionally-failing test inputs, not regressions.

Files modified:
- `PLang/App/Channels/Channel/Events/this.cs` — `_active` instance scope; `Enter` copy-on-write; `Releaser` rewritten.

Artifacts:
- `.bot/runtime2-channels/coder/v7/plan.md`
- `.bot/runtime2-channels/coder/v7/v6_review_summary.md`
- `.bot/runtime2-channels/coder/v7/baseline-tests.md`

## Code example
Before:
```csharp
private static readonly AsyncLocal<HashSet<string>?> _active = new();

public IDisposable Enter(string bindingId)
{
    var set = _active.Value ??= new HashSet<string>();
    set.Add(bindingId);
    return new Releaser(set, bindingId);
}
```

After:
```csharp
private readonly AsyncLocal<HashSet<string>?> _active = new();

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

## For v7 after review
v3 review flagged:
- B1 (`_active` static) — fixed by removing `static`.
- L1 (mutable AsyncLocal hazard) — fixed by copy-on-write + parent-restore on dispose.
- I1 (`Variables.Snapshot()` overlay-blind) — deferred; only Stage-9 stub calls it.

## Next
Suggest **codeanalyzer** to verify the fixes.
