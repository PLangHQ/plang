# Learnings — runtime2-callstack — security v1

## OBP `@this` convention details

- `App/CallStack/Call/this.cs` declares `namespace App.CallStack.Call;` and `public sealed partial class @this`. Consumers reference it as `App.CallStack.Call.@this`. Inside the parent (CallStack) namespace they alias `Call = App.CallStack.Call.@this`.
- Generator's `Action.@this` is the canonical action entity — referenced everywhere as `App.Goals.Goal.Steps.Step.Actions.Action.@this`. Module files alias it `using ActionEntity = ...Action.@this;` to keep call sites readable.
- The same pattern shows up for `Errors/this.cs` (the `App.Errors.@this` namespace root) — file is named `this.cs`, class name is `@this`.

## Cycle detection enforcement (this branch flipped advisory → mandatory)

- `MaxDepth` defaults to 1000. Walks Caller chain on every Push. O(d) per push.
- `ContainsGoal` walks from `_current.Value` up via Caller, comparing PrPath case-insensitively. Identity is `action.Step?.Goal?.PrPath` — Path is build-time, PrPath is the stable goal identity.
- Goal-cycle check only fires when crossing INTO a different goal than the caller — same-goal sequencing (elseif, retry, foreach body) doesn't trip.
- Both throw `CallStackOverflowException`, caught at App.Run AND Goal.RunAsync Push sites and converted to `ServiceError("CallStackOverflow", 500)`. Without this catch, the cycle exception would leak as a raw CLR exception.

## AsyncLocal scope discipline

- `App.CallStack.@this._current` and `App.Errors.@this._current` are **instance-level** AsyncLocal, not static. The reason: tests construct multiple Apps in one process; static would make them see each other's Current. Codeanalyzer/v3 fixed this regression — worth remembering it as a recurring static-vs-instance trap.
- `RestoreCurrent(leaving, previous)` only flips back if leaving == _current.Value. Parallel sibling branches that have set their own Current don't get clobbered.
- `Errors.Push` returns an IDisposable that captures previous AsyncLocal value and restores on Dispose. The `using` site is the discipline — restorer is also `_disposed`-guarded so double-Dispose is safe.

## Run-wide accumulator anti-pattern (THIS BRANCH)

- `stack.Audit : List<IError>` and `app.Errors.All : List<IError>` are RUN-WIDE accumulators — survive Pop, persist for App lifetime.
- They are written from multiple call sites (App.Run x2, Goal.RunAsync x1, Errors.Push x1). NONE of them have synchronization.
- Under Task.WhenAll on goal.call (the architect plan's stated parallelism vector), concurrent Add → ArgumentOutOfRangeException, dropped entries, or corrupt _size field. **Standing pattern**: if a list is shared across AsyncLocal forks, it MUST be guarded.
- Fix shape: dedicated private lock object, `lock(_auditLock) { Audit.Add(err); }`. Or `ConcurrentBag<IError>` if ordering is not load-bearing.

## lock() on publicly-visible List

- `lock(caller.Children)` and `lock(Diffs!)` lock on List instances exposed via public properties. External code that legitimately takes its own lock on the same instance can deadlock.
- Convention: `private readonly object _lock = new()` and `lock(_lock)`. The architect plan didn't call this out; codeanalyzer/v3 didn't flag it either. Worth catching in future audits — it's one grep away (`lock\s*\(\s*\w+\.[A-Z]`).

## CallChainRenderer reference-equality assumption

- `IsCauseBoundary` compares `chain[i].Cause` to `chain[i+1].Cause` by `ReferenceEquals`. Works because `Cause => _ownCause ?? Caller?.Cause` returns the same instance walking up. If Cause ever becomes a virtual property that reconstructs (e.g. lazy resolve), the boundary detection silently breaks. Coder/v2 risk-registered this.
- Lesson: when ref-equality is load-bearing, the property's identity stability is part of the contract. Note in security report any future refactor that makes Cause virtual.

## error.handle.Wrap retry vs recovery semantics

- Order = RetryFirst (default): tries Retry → Recovery → IgnoreError.
- Order = GoalFirst: tries Recovery → Retry → IgnoreError.
- Recovery success → `erroredCall.Handled = true`, error stays in audit (NOT discarded). Renderers show "errored — recovered."
- Recovery failure → `result.Error.ErrorChain.Add(recoveryError)`. Original error keeps audit, recovery error chains.
- IgnoreError is the FINAL fallback after retry+recovery. Past misreadings put it earlier — confirming order here.

## Variables.OnSet handler lifecycle

- Subscribe in Call.@this ctor (during Push) only when `Flags.Diff && diffSource != null`.
- Unsubscribe in DisposeAsync.
- Subscribe-before-Push, unsubscribe-after-Pop is the discipline. Push has no try/catch around subscription, but ctor body has Diff null check inside — subscription happens last, no leak path.
- If multiple nested Calls subscribe (deep nesting + diff:true), every Variables.Set fires O(N) handlers. Documented design choice — gated by diff flag.

## Verbose mode + [Sensitive]: pre-existing gap

- `Error.cs FormatVerboseValue` calls `JsonSerializer.Serialize(value)` with no options. [Sensitive] is NOT stripped.
- `Debug.cs _debugJsonOptions` DOES use SensitivePropertyFilter — but it's for the per-step variable dump, not the error verbose dump.
- `AssertSnapshot.cs:17` writes `Variables.Snapshot()` to AssertionError.Variables raw — same pre-existing concern.
- Don't flag as "new on this branch" — only the CallFrame→Call rename touched Error.cs. Note as carry-forward in security-report.

## Runtime/security artifact paths

- This branch's PLang tests under `Tests/App/CallStack/` — Audit, CauseLink, CrossFileChain, Tag*, Depth*, Cycle* test goals. Useful for repro of cycle/recovery scenarios.
- Tester's stale-binary lesson: ALWAYS clean rebuild PlangConsole before invoking PLang tests. The bot runner inherits binaries across sessions; `dotnet run --project PLang.Tests` is fine, but the PLang suite uses a pre-built PlangConsole binary.
