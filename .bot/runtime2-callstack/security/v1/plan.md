# security — runtime2-callstack — v1 plan

## Subject under audit

The callstack refactor delivered by `architect/v1` → `coder/v2`. Phases 1-11
landed: ConcurrentStack → AsyncLocal tree, `CallFrame` → `Call.@this`,
`Cause` causality lane, `App.Errors.@this` AsyncLocal scope replacing
`Context.Error`, Variables collection-level events, Diff capture (scalar +
deepDiff), Tags surface (C# + PLang `tag` action), CallChainRenderer.

`tester/v1/correction.md` confirmed PLang+C# suites green after a clean
rebuild. Codeanalyzer/v3 PASS. No prior security pass on this branch.

## Threat model relativisation

Per PLang's user-sovereign model, the .pr is the trust boundary. A
malicious `- tag` loop or recursive `goal.call` is the user's prerogative
and not an attack. Where security genuinely matters here:

1. **Concurrency safety of run-wide accumulators** under `Task.WhenAll`
   on `goal.call` — AsyncLocal forks naturally but the lists it points at
   (`stack.Audit`, `app.Errors.All`, `Caller.Children`, `Call.Tags`) are
   shared and not all of them are guarded.
2. **Cycle detection enforcement** — newly mandatory at Push, must trip
   reliably. Bypass means stack overflow / unbounded memory.
3. **Information disclosure** — `error.Variables`, the verbose dump path,
   and the `error.CallFrames` snapshot retain references to Goal/Step/
   parameters; if errors are serialized to logs/CI, these can carry
   sensitive payloads. (Standing finding category for this branch's diff.)
4. **AsyncLocal scope discipline** — `App.Errors.Push` must restore even
   under exception; missed restore would leak the caught error into
   sibling scopes.

What is explicitly **out of scope** for this audit (covered earlier or
non-callstack):
- Signing / Ed25519 / Variable+IRawNameResolvable migration (already
  audited on `runtime2-generator-obp`).
- Builder LLM prompts / source-resolution issues.
- `Module.add` and provider loading (accepted-risk: trust boundary is .pr).

## Audit plan

### Phase 1: Blue team — map exposure of the new surfaces

For each, log: exposure, trust boundary, mitigations, gaps.

- **CallStack.Push / Call ctor / DisposeAsync** — AsyncLocal flow,
  cycle detection, Children list mutation, OnSet subscription lifecycle.
- **App.Errors.Push** — AsyncLocal scope, restorer correctness, race-free
  All accumulator?
- **error.handle.Wrap** — retry + recovery scope, Cause threading,
  Handled flag set semantics.
- **debug.tag handler** — target = Caller's Tags dict, parallel write
  hazard.
- **CallChainRenderer** — pure function but consumes user-controlled
  goal/path strings; check for injection into rendered string.
- **Error.cs Format / Verbose mode** — variable dump filtering (does it
  honor [Sensitive]?), CallFrames serialization.
- **Variables collection events** — OnSet/OnCreate/OnRemove subscriber
  list. Reentrancy on Set inside a handler.
- **CallStackFlags parser** — `--debug={callstack:{...}}` JSON parse,
  malformed input handling.

### Phase 2: Red team — concrete vectors

1. **Audit.Add race**: parallel goal.call branches each erroring →
   `List<IError>.Add` from multiple threads → ArgumentOutOfRangeException
   or silent loss.
2. **app.Errors.All race**: same pattern in `Errors.@this.Push`.
3. **Tag race**: parallel foreach iterations dispatching `tag` resolve
   to the same Caller, race on `Tags ??= new Dictionary` and dictionary
   write.
4. **Public-list lock (Children, Diffs)**: external code reading
   `call.Children` could take its own lock and deadlock with Push/Pop.
5. **Cycle detection bypass via null PrPath**: synthesized actions
   without `Step.Goal.PrPath` skip ContainsGoal — relies on MaxDepth
   only; legitimate but worth verifying.
6. **`error.Variables` info disclosure**: AssertSnapshot populates with
   `Variables.Snapshot()` (already a standing medium finding); confirm
   this branch didn't widen the surface.
7. **Verbose dump unfiltered**: `Error.cs:228-247` walks all vars on
   error when `Debug.Verbose` is on. Does it strip `[Sensitive]`?
   (Compare with `_debugJsonOptions` which DOES use SensitivePropertyFilter.)
8. **CallFrames retention**: Cause references pin errored Calls into
   recovery descendants; document, ensure no surprising memory pin.
9. **OnSet handler exception swallowed?**: if `_onSetHandler` raises,
   does it propagate into Variables.Set's call site and break user
   code? (Event invocation semantics in C#.)
10. **Push exception ordering**: cycle-detection throw happens before
    OnSet subscription, so no leak there. Verify the path that
    constructs `Call` then mutates `caller.Children` under lock — if
    lock throws (it can't on a `lock(object)` in .NET, but defensively),
    OnSet would have already been subscribed by ctor.

### Phase 3: Severity rating + write findings

Every finding lands in `security-report.json`. Verdict:
- pass if no critical/high open
- fail if any critical/high open

### Phase 4: Memory updates + commit

- Update memory if any callstack-specific patterns deserve standing
  guidance (e.g. "run-wide accumulators need lock or `ConcurrentBag`").
- Write `summary.md`, root `summary.md`, finalize `report.json`.
- Commit `.bot/`, push branch.

## What I'm NOT going to do

- Re-audit signing / Variables/IRawNameResolvable. Already done on
  `runtime2-generator-obp` (open issues 4 are tracked there).
- Generic dead-code review. That's auditor/codeanalyzer territory.
- File-system path traversal. The new code doesn't do file I/O.
