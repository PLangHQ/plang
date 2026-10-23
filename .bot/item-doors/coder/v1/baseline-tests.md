# Baseline — item-doors stage 1 (HEAD d66967ee3, clean tree)

`./dev.sh test` (full C# sweep), one run. PLang tests: deferred to handoff gate.

## C# suites
| Suite | failed | notes |
|---|---|---|
| Modules | 0 | green |
| Types | 2 | **stage-1 reds** (go green this stage) |
| Wire | 6 | **pre-existing** — known runtime2 snapshot/resume baseline (not mine) |
| Data | 0 | green |
| Generator | 0 | green |
| Runtime | 2 | one **stage-1 red**, one **pre-existing** |

## The 3 stage-1 reds (test-designer's contract — must go green)
- `PLang.Tests.Types` → **ImageFromBytes_Kind_CarriesItsRegisteredMime** — `img.Type.kind.Mime` should contain `image/png`.
- `PLang.Tests.Types` → **PlainText_Kind_IsText_WithoutNameFloor** — `t.Type.kind.IsText` should be true.
- `PLang.Tests.Runtime` → **AFreshTypeByNameCarriesNoFacts_TheRegisteredOneDoes** — `new type.@this("action").ClrType == _app.type.list["action"].ClrType`.

## Pre-existing reds (NOT mine — green-before unaffected, verify unchanged at handoff)
- `PLang.Tests.Runtime` → **TheActorNamed_RunsIt_AndListsIt** — pre-existing on HEAD; unrelated to born-whole.
- `PLang.Tests.Wire` (6): MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal,
  NavigateAndEditCapturedVariable_ThenResumeToSuccess, TypedSnapshotString_NavigateEditResume_PersistsEdit,
  SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess, ThrowTimeSnapshot_EditSurvivesResume,
  (+1). Known runtime2 snapshot/resume baseline.

Any green-before test going red is my regression. The 3 reds → green is the stage's proof.
