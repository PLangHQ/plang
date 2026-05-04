# Approved Test Plan — runtime2-callstack — v1

## C# (TUnit) — `PLang.Tests/`

| File | Tests |
|---|---|
| `App/CallStack/CallTests.cs` | 10 |
| `App/CallStack/CallStackTreeTests.cs` | 10 |
| `App/CallStack/AsyncLocalForkTests.cs` | 6 |
| `App/CallStack/CauseLinkageTests.cs` | 6 |
| `App/CallStack/CycleDetectionTests.cs` | 5 |
| `App/CallStack/CallStackFlagsTests.cs` | 8 |
| `App/CallStack/ItemsExtensionTests.cs` | 5 |
| `App/CallStack/DiffCaptureTests.cs` | 7 (incl. memory-bound test absorbed from PLang P8) |
| `App/CallStack/CallStackAuditTests.cs` | 5 |
| `App/CallStack/SnapshotChainTests.cs` | 5 |
| `App/Errors/ErrorsScopeTests.cs` | 7 |
| `App/Variables/CollectionEventsTests.cs` | 6 |
| `App/Modules/debug/TagActionTests.cs` | 5 |
| `App/Debug/DebugCallStackParseTests.cs` | 6 |
| `App/Errors/ServiceErrorChainTests.cs` | 4 |

**Total: 95 C# tests across 15 files.** All bodies stubbed with `Assert.Fail("Not implemented")`.

## PLang `--test` — `Tests/App/CallStack/`

One goal per `.test.goal` file (per project rule). Each body is `- throw "not implemented"`.

| File | Goal |
|---|---|
| `DepthIncreasesOnGoalCall.test.goal` | TestDepthIncreasesOnGoalCall |
| `DepthRestoresAfterCall.test.goal` | TestDepthRestoresAfterCall |
| `CrossFileChain.test.goal` | TestErrorCallFramesContainsDeepNestedChain |
| `Audit.test.goal` | TestAuditAccumulatesHandledAndUnhandledErrors |
| `OuterErrorVarVisibleAfterInnerHandlerCloses.test.goal` | TestOuterErrorVarVisibleAfterInnerHandlerCloses |
| `InnerErrorVarShadowsOuter.test.goal` | TestInnerErrorVarShadowsOuter |
| `ErrorVarIsNullOutsideHandler.test.goal` | TestErrorVarIsNullOutsideHandler |
| `CauseLink.test.goal` | TestRecoveryActionCauseIsErroredAction |
| `Diffs.test.goal` | TestVariableDiffsCapturedInDiffMode (needs `--debug={callstack:{diff:true}}`) |
| `TimeoutProducesCancellation.test.goal` | TestTimeoutAfterModifierProducesCancellationError (uses `timeout after 100ms` modifier) |
| `DirectGoalCycleThrows.test.goal` | TestDirectGoalCycleThrows |
| `IndirectGoalCycleThrows.test.goal` | TestIndirectGoalCycleThrows |
| `TagWritesPairsOntoCurrentCall.test.goal` | TestTagWritesPairsOntoCurrentCall (needs `--debug={callstack:{tags:true}}`) |
| `TagBareLabelWritesTrue.test.goal` | TestTagBareLabelWritesTrue (needs `--debug={callstack:{tags:true}}`) |
| `HandledFlagSetWhenRecoverySucceeds.test.goal` | TestHandledFlagSetWhenRecoverySucceeds |
| `HandledFlagFalseWhenRecoveryFails.test.goal` | TestHandledFlagFalseWhenRecoveryFails |

**Total: 16 PLang test goals across 16 files.**

## Decisions made during planning

- **P8 OOM safety** — moved from PLang to C# (`DiffCaptureTests.Diff_DiffModeOverLargeListDoesNotOom`). PLang `--test` can't measure RSS naturally; C# uses `GC.GetTotalMemory` snapshot delta.
- **P9 cancellation** — kept on PLang side via the existing `timeout after` step modifier. No new cancel primitive needed.
- **Old test goals removed** — `Tests/App/CallStack/CallStack.test.goal`, `Start.goal`, `Inner.goal`, `InnerTest.goal` all targeted the deprecated `%!callStack.Depth%` API and have been deleted (incl. their `.build/` cache).
- **Existing `PLang.Tests/App/Core/CallStackTests.cs`** — kept as-is. Coder will delete/replace it when they remove the `IsEnabled`, `Frame.Parent`, `EventId`, `Phase` shape.

## Suggested next bot

**coder** to implement Phases 1–9 of the architect plan and make these tests pass.
