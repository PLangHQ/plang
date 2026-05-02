# test-designer — runtime2-callstack — v1

## Goal

Translate the architect's plan (`.bot/runtime2-callstack/architect/v1/plan.md`) into a complete test contract for the coder bot. Tests define the spec; bodies are stubs (`Assert.Fail` / `throw "not implemented"`).

## Scope source-of-truth

- New types: `Call.@this`, `CallStackFlags`, `Diff`, `App.Errors.@this`, `tag` action handler.
- Renamed/relocated: `CallFrame` → `Call.@this`; `App.Debug.CallStack` (was `Actor.Context.CallStack`).
- Behavioral: Cause linkage, AsyncLocal forking, recovery via Cause (no synthetic frame), `Handled` flag, `%!error%` from `app.Errors.Error`, Variables collection-level events drive Diff capture, ServiceError chain composition tweak, cycle detection (`MaxDepth` + `ContainsGoal`).
- Out: renderer changes; disposable lifecycle (deleted dead code).

## Test plan — all batches

**C# (TUnit) — `PLang.Tests/`**

| # | File | Tests | Focus |
|---|---|---|---|
| 1 | `App/CallStack/CallTests.cs` | ~10 | `Call.@this` shape: Id, Caller/Cause/Children pointers, Errors list, Handled flag, IAsyncDisposable pop, SnapshotChain ordering. |
| 2 | `App/CallStack/CallStackTreeTests.cs` | ~10 | Push/Pop tree mechanics, Root, Current via AsyncLocal, Children allocate-always, history flag retention, MaxFrames cap. |
| 3 | `App/CallStack/AsyncLocalForkTests.cs` | ~6 | Parallel branches via `Task.WhenAll` don't pollute each other; AsyncLocal restore on dispose. |
| 4 | `App/CallStack/CauseLinkageTests.cs` | ~6 | `cause:` parameter on Push wires `Cause` field; null Cause for normal call; Cause keeps target alive (GC pin). |
| 5 | `App/CallStack/CycleDetectionTests.cs` | ~5 | MaxDepth throws with chain path; ContainsGoal direct cycle; indirect (A→B→A); recursive same-goal trips ContainsGoal. |
| 6 | `App/CallStack/CallStackFlagsTests.cs` | ~8 | Default all-false; shorthand `callstack:true` parse; per-flag JSON parse; flag gating (Diffs null when off, etc.). |
| 7 | `App/CallStack/ItemsExtensionTests.cs` | ~5 | `Get<T>`/`Set<T>` typed bag; lazy-allocate; multiple types coexist; null when not set. |
| 8 | `App/CallStack/DiffCaptureTests.cs` | ~7 | Variables.OnSet → Call.Diffs append when diff:true; null when off; scalar-only by default; deepDiff opt-in clones; unsubscribe on Dispose. |
| 9 | `App/CallStack/CallStackAuditTests.cs` | ~5 | Audit accumulator collects every error including handled; survives Pop. |
| 10 | `App/CallStack/SnapshotChainTests.cs` | ~5 | Returns `[this, Caller, ..., Root]`; single-frame returns `[this]`; refs are stable (no copy). |
| 11 | `App/Errors/ErrorsScopeTests.cs` | ~7 | `Push(error)` LIFO; nested scopes restore prev; `Error` AsyncLocal-flowed; `All` accumulator; null outside any scope. |
| 12 | `App/Variables/CollectionEventsTests.cs` | ~6 | `OnSet`/`OnCreate`/`OnRemove` fire at right sites; before/after carried; per-variable events still fire (back-compat). |
| 13 | `App/Modules/debug/TagActionTests.cs` | ~5 | `tag` action handler: Pairs dict merges into Current.Tags; Label form sets `Tags[label]="true"`; no-op when Current null; not cacheable. |
| 14 | `App/Debug/DebugCallStackParseTests.cs` | ~6 | `--debug={callstack:true}` shorthand; `{callstack:{timing:true,...}}` full form; `--debug` no callstack key → all-false; maxFrames default 1000; bad JSON fallback. |
| 15 | `App/Errors/ServiceErrorChainTests.cs` | ~4 | `ServiceError.CallFrames` typed `IReadOnlyList<Call.@this>`; `chain[0]` is the failing call; chain walks Caller. |

C# total: ~95 tests across 15 files.

**PLang `--test` — `Tests/App/CallStack/`** (replaces existing thin `CallStack.test.goal`)

| # | File | Goals | Focus |
|---|---|---|---|
| P1 | `Depth.test.goal` (+ helpers) | `TestDepthIncreasesOnGoalCall`, `TestDepthRestoresAfterCall` | Caller-walk depth — replaces old `%!callStack.Depth%` style with `Caller`-chain length. |
| P2 | `CrossFileChain.test.goal` (+ helpers) | `TestErrorCallFramesContainsDeepNestedChain` | Error in deep nested cross-file chain — `error.CallFrames` reflects full dynamic chain. |
| P3 | `Audit.test.goal` (+ helpers) | `TestAuditAccumulatesHandledAndUnhandledErrors` | 3 handled + 1 unhandled in one foreach iteration → `Audit.Count == 4`; `Handled` flags reflect outcomes. |
| P4 | `ErrorVarNesting.test.goal` (+ helpers) | `TestOuterErrorVarVisibleAfterInnerHandlerCloses`, `TestInnerErrorVarShadowsOuter` | Outer Wrap → inner Wrap → `%!error%` reads inner; after inner closes, outer's `%!error%` still its own. |
| P5 | `ErrorVarOutsideScope.test.goal` | `TestErrorVarIsNullOutsideHandler` | Outside any Wrap, `%!error%` is null. |
| P6 | `CauseLink.test.goal` (+ helpers) | `TestRecoveryActionCauseIsErroredAction` | Recovery body action's `%!callStack.Current.Cause.Action.Module%` is the errored action's module. |
| P7 | `Diffs.test.goal` (+ helpers) | `TestVariableDiffsCapturedInDiffMode` | Set+modify variable across steps → `%!callStack.Current.Caller.Children[1].Diffs[0].Before == "ingi"`. (Run with `--debug={callstack:{diff:true}}`.) |
| P8 | `OomSafety.test.goal` (+ helpers) | `TestDiffModeOverLargeListDoesNotOom` | 100-iter loop touching 1MB list under `--debug={callstack:{diff:true}}` does not exceed memory threshold. (Scalar-only default.) |
| P9 | `Cancellation.test.goal` (+ helpers) | `TestCancelledFrameHasOperationCancelledError` | Cancel mid-foreach; errored frame's `Errors[0]` is `OperationCanceledException`-shaped IError. |
| P10 | `Cycle.test.goal` (+ helpers) | `TestDirectGoalCycleThrows`, `TestIndirectGoalCycleThrows` | A→A and A→B→A both throw `CallStackOverflowException`. |
| P11 | `TagAction.test.goal` (+ helpers) | `TestTagWritesPairsOntoCurrentCall`, `TestTagBareLabelWritesTrue` | The new `tag` PLang action populates `Current.Tags`. |
| P12 | `Handled.test.goal` (+ helpers) | `TestHandledFlagSetWhenRecoverySucceeds`, `TestHandledFlagFalseWhenRecoveryFails` | `Handled` reflects recovery outcome; chained recovery error appended via `ErrorChain`. |

PLang total: ~17 goals across 12 `.test.goal` files (plus per-test helper goal files).

## Working approach

1. Present batches of ~10 tests. Each batch lists test name + one-line intent.
2. Wait for user approval per batch; incorporate feedback.
3. After all batches approved → write the actual test files (signatures + `Assert.Fail` / `throw "not implemented"`).
4. Commit + push. Suggest **coder** next.

## Design notes / open questions

- **Renamed type usage in C# tests** — existing `CallStackTests.cs` uses `Frame.Parent`, `Frame.EventId`, `IsEnabled`, etc. Those are gone. The new test files are designed against the new shape; existing file will be rewritten/replaced by the coder when they delete the old types.
- **PLang test goals can read `%!callStack%`** — current syntax uses `%!callStack.Depth%`. Plan renames frame collection but keeps `%!callStack%` as the alias in `Actor.Context`. Tests assume `%!callStack.Current` and `.Caller` walks work from PLang.
- **Diff mode CLI** — `--debug={callstack:{diff:true}}`. Need an explicit step in the test goal to flip it, OR run `plang --test --debug=...`. Per test runner conventions, use `--debug` env at invocation. PLang tests targeting diff/history flags must document the required CLI flag in a comment header.
- **OOM safety test** — needs assert of process memory. PLang tests usually don't measure RSS directly. May need a C#-side memory probe via `GC.GetTotalMemory(false)` snapshot before/after — propose moving #P8 into the C# suite (`DiffCaptureMemoryTests.cs`) instead. Will flag in the batch.
- **Cancellation test** — currently no PLang-level cancel primitive in the public surface. May require a `runtime.cancel` test hook OR convert to C#-side test calling Run with a CancellationToken. Will flag in the batch.

## Output

- `test-plan.md` — finalized once batches approved.
- Test files: under `PLang.Tests/App/CallStack/`, `PLang.Tests/App/Errors/`, `PLang.Tests/App/Variables/`, `PLang.Tests/App/Modules/debug/`, `PLang.Tests/App/Debug/` and `Tests/App/CallStack/` for PLang tests.
- `verdict.json` `{ "pass": true }`.
- `summary.md` (v1 + bot-root).
