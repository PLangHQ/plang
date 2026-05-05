# codeanalyzer v1 — runtime2-callback

**Scope:** All C# added or changed on this branch vs. `runtime2`.
**Verdict:** PASS with 2 fixable lows and 3 notes.

---

## Findings

### F1 — Medium: Unchecked index access in `AskCallback.PositionWire.Resolve`
**File:** `PLang/App/Callback/AskCallback.cs:121-122`

```csharp
var step = goal.Steps[StepIndex];       // throws ArgumentOutOfRangeException if out of range
var action = step.Actions[ActionIndex]; // same
```

`CallStack.this.Snapshot.cs:132-137` has the correct guards:
```csharp
if (stepIndex < 0 || stepIndex >= liveGoal.Steps.Count)
    throw new CallbackGoalNotFound(...);
```

`PositionWire.Resolve` does not. A wire payload with a bad index throws an uncaught CLR exception rather than a controlled `CallbackGoalNotFound`. A malformed (or tampered, while crypto is identity) callback can surface as an unhandled crash instead of a clean error return.

**Fix:** Mirror the same guards from `CallStack.Restore` into `PositionWire.Resolve` before both index accesses.

---

### F2 — Low: Infrastructure variable injection via wire (`AskCallback`)
**File:** `PLang/App/Callback/AskCallback.cs:73`

```csharp
foreach (var v in Variables)
    ctx.Variables.Set(v.Name, v.Value);
```

`Name` comes from the wire (via `VariableWire.ToData`). A crafted payload can inject `!`-prefixed infrastructure variable names (`!app`, `!error`, `!ask.answer`, etc.). While the crypto.encrypt is v1 identity today the vector is wide open; with real crypto it shrinks to key compromise.

**Fix:** Skip `!`-prefixed names on restore (mirrors `Variables.Snapshot.Capture` which already skips them on the outbound side):
```csharp
foreach (var v in Variables)
{
    if (!string.IsNullOrEmpty(v.Name) && !v.Name.StartsWith("!"))
        ctx.Variables.Set(v.Name, v.Value);
}
```

---

### N1 — Note: `ErrorCallback.Position` is always null before `Run`
**File:** `PLang/App/Callback/ErrorCallback.cs:22-31`

The getter unconditionally returns `null`. `_position` is only set inside `Run()`. This breaks the `ICallback` contract ("where does the resumed run land?") for any caller that reads `Position` before invoking `Run`. `AskCallback.Position` is init-set and available immediately.

No runtime breakage today (nothing in the codebase reads `ICallback.Position` before calling `Run`), but the contract is misleading. Consider computing `Position` from the snapshot's CallStack section at `Deserialize` time, or documenting explicitly that it materialises only after `Run`.

---

### N2 — Note: `SnapshotAt` drops `Type` and `Properties` from restored variables
**File:** `PLang/App/Variables/this.SnapshotAt.cs:25`

```csharp
clone._variables[diff.Name] = new Data.@this(diff.Name, diff.Before) { Context = _context };
```

The restored entry carries only `Name` + `Value`. The original Data's `Type` and `Properties` are silently dropped. Error handlers that read `%x%.Type` or Properties on a SnapshotAt-projected variable see null/default. Value-only time-travel is correct for the current tests, but the silent gap will bite if error handlers ever introspect types. Document the known narrowness.

---

### N3 — Note: `CallbackGoalHashMismatch`/`CallbackGoalNotFound` are CLR exceptions, not `IError`
**Files:** `PLang/App/Errors/CallbackGoalErrors.cs`, `PLang/App/CallStack/this.Snapshot.cs:124-129`

Both propagate as uncaught CLR exceptions out of `Restore` and `Deserialize`. Every call site must `catch` them or they surface as unhandled crashes. The rest of the system uses `Data.FromError(IError)` for controlled failure. These error types read like hard invariants (which is the right intent), but callers using them in the error-retry flow need explicit try/catch. Consider noting this at the public entry points.

---

## OBP Check

All new types pass:

| Type | Verdict |
|---|---|
| `Snapshot.@this` | ✅ Both dictionaries private; `Section`/`Write`/`Read` surface is the only entry point |
| `AskCallback.Variables` | ✅ `init`-only — no external mutation after construction |
| `ErrorCallback.AppSnapshot` | ✅ `init`-only |
| `CallStack._streamDiffs` | ✅ Private field, lock-protected, no external mutation |
| `CallStack._restoredChain` | ✅ Private field, only set via the type's own static `Restore` |
| `Errors._current` | ✅ `AsyncLocal<IError?>` owned and managed entirely inside `Errors.@this` |
| `App.Statics` (new `AppStatics`) | ✅ `GetBag` accessor; bags are `ConcurrentDictionary` — thread-safe |

One borderline: `Errors.this.cs:27` — `internal App.@this? App { get; set; }`. The setter is internal and only used in `App.@this` ctor (`Errors.App = this`). Acceptable — the set path is effectively single-site and App owns Errors.

---

## Sync-over-async (endorsed design decision)

`AskCallback.Serialize`, `ErrorCallback.Serialize`, and `Data.EnsureSigned` all use `.GetAwaiter().GetResult()`. The architect endorsed this for `Signature`; the coder extended it symmetrically. Tracked as Decision #6 in the handoff. Not a new finding — noting for completeness.

---

## Verdict

| Finding | Severity | Blocking? |
|---|---|---|
| F1 — unchecked index in PositionWire.Resolve | Medium | Yes — fix before merge |
| F2 — infra-var injection via wire | Low | Recommend fix; not blocking while crypto is identity |
| N1 — ErrorCallback.Position contract | Note | Document or fix in stage-5 |
| N2 — SnapshotAt drops Type/Properties | Note | Document the known narrowness |
| N3 — Hard errors as CLR exceptions | Note | Document at call sites |

**F1 must be fixed before merge.** F2 recommended alongside F1 (two lines). N1-N3 are documentation or deferred items.

C# test suite: 2720/2720 per coder summary — not re-run (stale-binary trap; full rebuild needed before declaring final green).
