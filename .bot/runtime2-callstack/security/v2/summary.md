# security — runtime2-callstack — v2

## What this is

Re-audit after coder's response to v1 findings. Subject:
commits `d2d9d2be` (OBP refactor — promote `IError`/`Children` lists to
domain types) and `ec092e91`. Diff is narrowly scoped to the four
collections v1 flagged plus the `Call.Tag` write path and the stale
`_root` fix.

## Verdict

**pass.** Zero medium open. Two low residuals.

| v1 # | Title | Status |
|---|---|---|
| 1 | medium — Audit/All race | **closed** |
| 2 | low — Tag race | **closed** |
| 3 | low — public-list lock smell | **partial** (Children fixed; Diffs lock target fixed but reader still races — see F1) |
| 4 | low — unbounded growth | open (by-design per architect) |
| 5 | low — stale _root | **closed** |

| New | Severity | Title |
|---|---|---|
| F1 | low | Diffs reader race — `List<Diff>?` still raw; readers iterating during sibling-branch OnSet writes can throw `InvalidOperationException` |
| F2 | low | CallStack.Flags torn read — `record struct` reassigned via public setter, non-volatile field; soft concern |

## What was done

- Pulled `a6158e74..ec092e91`, rebuilt PLang from clean. Build clean, 0 errors.
- Walked the new domain classes side-by-side against v1's findings:
  - `App/CallStack/Audit/this.cs`, `App/Errors/Trail/this.cs`,
    `App/CallStack/Call/Errors/this.cs`, `App/CallStack/Call/Children/this.cs`
    — all four follow the same shape: domain class wrapping a
    `List<T>`, private `_lock`, snapshot iteration, `IReadOnlyList<T>`
    for natural PLang/`%!`-path access.
  - Tag fix: `Call/this.cs:156-163` — private `_tagsLock` covers
    `Tags ??= new()` AND the indexer write atomically.
  - Stale `_root`: `CallStack/this.cs:104-106` — reassigns on every
    top-level Push.
- Verified no regressions in cycle detection, AsyncLocal restore,
  Restorer disposability, OnSet handler exception shape.
- Confirmed test green: `ErrorsScopeTests.Trail_AccumulatesEveryPushedError`
  exercises the new shape and passed in coder's run.

## Code examples

The new pattern, applied uniformly across four collections:

```csharp
// PLang/App/CallStack/Audit/this.cs
public sealed class @this : IReadOnlyList<IError>
{
    private readonly List<IError> _entries = new();
    private readonly object _lock = new();

    public void Add(IError error) { lock (_lock) _entries.Add(error); }
    public int Count { get { lock (_lock) return _entries.Count; } }
    public IError this[int index] { get { lock (_lock) return _entries[index]; } }

    public IEnumerator<IError> GetEnumerator()
    {
        IError[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<IError>)snapshot).GetEnumerator();
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
```

The asymmetry — Diffs alone is still raw:

```csharp
// PLang/App/CallStack/Call/this.cs:98
public List<Diff>? Diffs { get; }                  // raw — reader race

// line 137-145, OnSet handler
_onSetHandler = (name, before, _) => {
    lock (_diffsLock) {                            // writer-only sync
        Diffs!.Add(new Diff(name, ...));
    }
};
```

A debug observer iterating `call.Diffs` while a sibling Task.WhenAll
branch fires OnSet on the same Variables instance can throw
`InvalidOperationException: Collection was modified`. Writes are safe;
reads are not.

## Recommendation

`pass` — branch is mergeable.

For consistency before parallel `goal.call` lands, recommend one more
coder pass to promote `Diffs` to a domain class `App.CallStack.Call.Diffs.@this`
mirroring the four siblings. Mechanical edit (~30 lines) and the only
remaining asymmetry in the new OBP shape. F2 (Flags torn read) is fine
to accept as documented.

## Files written

- `.bot/runtime2-callstack/security-report.json` (rewritten as v2)
- `.bot/runtime2-callstack/security/v2/plan.md`
- `.bot/runtime2-callstack/security/v2/verdict.json`
- `.bot/runtime2-callstack/security/v2/summary.md`
- `.bot/runtime2-callstack/security/summary.md` (updated)
