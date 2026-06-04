# tester — type-kind-strict

## Version
v13 (matches coder v13, re-test after the lazy-deserialize merge). Verdict: **PASS**.
Supersedes v8 (FAIL).

## What this is
The branch reshapes PLang's type value into a structured `{Name, Kind, Strict}`
entity, folds `Data.Kind`, adds strict-kind enforcement, and (via the merged
`lazy-deserialize` work) defers materialization with a reader registry + lazy
`Data`. coder v13 itself is **test-only** — two C# files closing carry-forward
gaps flagged by lazy-deserialize's auditor v2 / tester v3.

## Why v8 FAILed and why v13 PASSes
v8 FAIL was **reproducibility, not logic**: 688/703 committed `.pr` were stale vs
the branch's stage-4 `variable.set.Type` entity change, so `plang --test` flapped
0–4 fails from a clean binary and the reported "262/262" was a warm-cache artifact.
The `lazy-deserialize` merge (`d4fdd030c`) regenerated and committed the whole
`Tests/` tree against the entity shape — the exact fix I asked for. Now:

- **PLang 273/273/0, 0-stale, deterministic across 2 consecutive clean-binary runs,
  `git status` clean after each (zero `.pr` rewritten).** Blocker dead.
- **C# 4025/4025/0** on clean rebuild.

## What was done (v13)
- Ran the reproducibility gate that failed v8 — passed cleanly twice.
- Code-read both new test files; both assert intent (not `Data.Ok()`):
  `MaterialiseErrorPathTests` checks `Error.Key == "MaterializeFailed"` (distinct
  from `NotFound`) + source-name in message + no-throw-to-courier;
  `SignedDataSurvivesVariableSetListTests` verifies signature survives the real
  `variable.set` ShallowClone and `signing.verify` returns true.
- **Independently mutation-tested** the set-path `MaterializeFailed` contract:
  neutralizing the guard at `variable/list/this.cs:311` flips exactly the two
  set-path tests red; reverted clean. Tests genuinely catch the regression.
- Confirmed strict×lazy enforcement coverage survived the merge (`Cut2`,
  `LazyPathHandleTests`).

## Finding (one, minor, non-blocking)
Carry-forward of v8 F2: no end-to-end PLang goal exercises lazy strict image
set→load→throw (`SetAsImageGifStrictMismatch.test.goal` asserts only `Type.Name`/
`Kind`, never forces a byte load). The contract is pinned in C# instead. Optional
polish now that fixtures are stable.

## Code example (the honest assertion that earns the pass)
```csharp
var child = d.GetChild("host");                  // malformed-JSON parent
await Assert.That(child.Error!.Key).IsEqualTo("MaterializeFailed"); // not "NotFound"
```
Mutation-confirmed: remove the guard, this flips red.

## Files (this version)
- `v13/plan.md`, `v13/result.md`, `v13/verdict.json`
- `../test-report.json` (branch root, shared)
