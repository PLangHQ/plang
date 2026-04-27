# Tests/ Folder Structure Proposal (Phase 1)

**Status:** approved naming rule, specific layout awaiting final approval. Runs *first*, before the baseline — fixing tests in the old sprawl while also planning to move them is wasted motion. Structure first, then individual test work (add/modify/rename/remove) on the new layout.

## Naming rule

PLang uses two conventions for two purposes:
- **PascalCase** — named callable entity (goals). `Start.goal`, `MathIsSane.test.goal`.
- **lowercase** — namespace identifier in `module.action` syntax. `condition.if`, source `PLang/App/modules/condition/`.

`Tests/` is **not a namespace** — it's a tree of scenarios containing callable test goals. Files ARE goals, so PascalCase on files is mandatory. Folders follow the same rule for coherence: **PascalCase, no separator for multi-word** (`CompoundAnd`, not `compound_and`). Matches what Tests/ already mostly does — minimal churn.

**Fixtures stay lowercase** (`alpha.fixture.goal`, `failsvar.fixture.goal`) — they're test data, not callable entities. Existing convention, keep.

## What's wrong today

1. **Top-level mixes three kinds of thing** — per-module (Condition/, Http/), cross-cutting runtime (CallStack/, DeepNavigation/, RecursionDepthLimit/), meta-tests (Builder/, TestModule/). No rule tells you which is which.
2. **25+ tiny subfolders under Condition/** (Basic, Compound, CompoundAnd, CompoundOr, Contains, ElseBranch, EndsWith, Equals, Falsy, FileExists, ...) — flat sprawl, no grouping by concept.
3. **Redundant file names.** `Tests/Condition/Basic/Condition.test.goal` repeats "Condition".
4. **Cross-cutting concerns have no home.** Retry/Error-modifier tests are split across Error/, Retry/, and ad-hoc subdirs.
5. **`Tests/Runtime2/`** — contains only one stray `test_output.txt`, orphan artefact from a past `File.test.goal` run under a different working dir. Nothing references it. Delete the folder.

## Proposed structure

Three top-level buckets: **Modules/**, **App/**, **Builder/** (reserved). PascalCase everywhere (folders + files). Sub-scenarios grouped by feature cluster, not flat.

- `Modules/` — tests for any PLang module (mirrors source at `PLang/App/modules/`)
- `App/` — cross-cutting runtime/App behavior (mirrors `PLang/App/` in source, where c# calls this layer "App")
- `Builder/` — reserved top-level for future pipeline-level integration tests of the build process itself. Current contents of `Tests/Builder/*` are all `builder.X` module-action tests (confirmed) and move under `Modules/Builder/`.

Collisions on generic names (multiple `Basic.test.goal` across modules) are fine — the full path disambiguates and grep shows the path.

```
Tests/
  Modules/
    Assert/
      Basic.test.goal                      (was Assert/Assert.test.goal)
      Complete.test.goal                   (was Assert/AssertComplete.test.goal)
    Builder/                               (was top-level Builder/)
      GetActions.test.goal
      GetTypeInfo.test.goal
      ParseGoal.test.goal
      ValidateValid.test.goal
    Cache/
      Basic.test.goal
      Sliding.test.goal
      Key.test.goal
      DynamicKey.test.goal
    Condition/
      If/
        Basic.test.goal                    (was Condition/Basic)
        ElseBranch.test.goal
        ElseIf.test.goal                   (new, add when tests arrive)
        Nested.test.goal
        SubSteps.test.goal                 (merge SubStepsTrue + SubStepsFalse)
      Compound/
        And.test.goal, Or.test.goal, Mixed.test.goal
      Operators/
        Equals.test.goal, NotEquals.test.goal,
        GreaterThan.test.goal, LessThan.test.goal,
        GTE.test.goal, LTE.test.goal,
        Contains.test.goal, StartsWith.test.goal, EndsWith.test.goal,
        Not.test.goal, Truthy.test.goal, Falsy.test.goal
      Files/
        FileExists.test.goal, FileNotExists.test.goal, FileExistsSubSteps.test.goal
    Crypto/
      HashDefault.test.goal, HashBcryptVerify.test.goal,
      HashObject.test.goal, HashSHA256.test.goal,
      ProviderSwap.test.goal, VerifyWrongHash.test.goal
    Error/
      Call.test.goal, Chain.test.goal, GoalFirst.test.goal,
      Handling.test.goal, InHandler.test.goal, Mixed.test.goal,
      Multilingual.test.goal, Nested.test.goal, Ordering.test.goal,
      Props.test.goal, RetryOnly.test.goal, Types.test.goal
    Event/
      Basic.test.goal, AfterAction.test.goal, AfterStep.test.goal,
      BeforeStep.test.goal, Multiple.test.goal, Override.test.goal,
      Priority.test.goal, Remove.test.goal, Wildcard.test.goal
    File/
      Basic.test.goal
    Goal/                                  (was GoalCall/)
      Basic.test.goal, Dynamic.test.goal, Missing.test.goal,
      Relative.test.goal, Return.test.goal
    Http/
      GetRequest.test.goal, PostRequest.test.goal,
      DownloadFile.test.goal, DownloadSkip.test.goal, UploadFile.test.goal,
      SignedRequest.test.goal, UnsignedRequest.test.goal,
      ConfigBaseUrl.test.goal, ConfigHeaders.test.goal,
      StreamCallback.test.goal
    Identity/
      Create.test.goal, AutoCreate.test.goal, GetByName.test.goal,
      Rename.test.goal, Export.test.goal,
      ArchiveDefault.test.goal, ArchiveNonDefault.test.goal, Unarchive.test.goal,
      SwitchDefault.test.goal, DotNavigation.test.goal
    List/                                  (was ListOps/)
      Basic.test.goal, Advanced.test.goal
    Llm/
      Schema.test.goal
    Loop/                                  (merges Foreach/ + Loop/)
      Foreach/
        Basic.test.goal, Dictionary.test.goal, Empty.test.goal
      ...
    Math/
    Mock/
    Output/
    Settings/
    Signing/
    Test/                                  (was TestModule/)
      Discover/, Run/, Report/, Tag/, Assert/, Condition/,
      EdgeCase/, Orchestrate/
    Ui/
    Variable/
      Basic.test.goal                      (was Variable/)
      ContextVars/                         (was ContextVars/ — context vars ARE variables)
        Basic.test.goal, System.test.goal, Advanced.test.goal
      FromJson/                            (was FromJson/)

  App/                                     (mirrors c# PLang/App/ layer)
    Actors/                                (was Actor/ — Context, Datasource, Switch)
    CallStack/
    DeepNavigation/
    RecursionDepth/                        (was RecursionDepthLimit/)
    ReturnMapping/
    StartupParams/
    StepResult/
    SetupGoal/
    Retry/                                 (retry is a modifier, not a module)

  Builder/                                 (reserved for pipeline-level integration tests)
```

## Mapping — old → new

| Old path | New path |
|---|---|
| `Tests/Actor/` | `Tests/App/Actors/` |
| `Tests/Assert/` | `Tests/Modules/Assert/` |
| `Tests/Builder/` | `Tests/Modules/Builder/` (all current contents are module-action tests) |
| `Tests/Cache/` | `Tests/Modules/Cache/` |
| `Tests/CallStack/` | `Tests/App/CallStack/` |
| `Tests/Condition/*` | `Tests/Modules/Condition/{If,Compound,Operators,Files}/` (regrouped) |
| `Tests/ContextVars/` | `Tests/Modules/Variable/ContextVars/` |
| `Tests/Crypto/` | `Tests/Modules/Crypto/` |
| `Tests/DeepNavigation/` | `Tests/App/DeepNavigation/` |
| `Tests/Error/` | `Tests/Modules/Error/` |
| `Tests/Event/` | `Tests/Modules/Event/` |
| `Tests/File/` | `Tests/Modules/File/` |
| `Tests/Foreach/` | `Tests/Modules/Loop/Foreach/` |
| `Tests/FromJson/` | `Tests/Modules/Variable/FromJson/` |
| `Tests/GoalCall/` | `Tests/Modules/Goal/` |
| `Tests/Http/` | `Tests/Modules/Http/` |
| `Tests/Identity/` | `Tests/Modules/Identity/` |
| `Tests/ListOps/` | `Tests/Modules/List/` |
| `Tests/Llm/` | `Tests/Modules/Llm/` |
| `Tests/Loop/` | `Tests/Modules/Loop/` (merge with Foreach) |
| `Tests/Math/` | `Tests/Modules/Math/` |
| `Tests/Mock/` | `Tests/Modules/Mock/` |
| `Tests/Output/` | `Tests/Modules/Output/` |
| `Tests/RecursionDepthLimit/` | `Tests/App/RecursionDepth/` |
| `Tests/Retry/` | `Tests/App/Retry/` |
| `Tests/ReturnMapping/` | `Tests/App/ReturnMapping/` |
| `Tests/Runtime2/` | **DELETE** (only an orphan `test_output.txt`) |
| `Tests/Settings/` | `Tests/Modules/Settings/` |
| `Tests/SetupGoal/` | `Tests/App/SetupGoal/` |
| `Tests/Signing/` | `Tests/Modules/Signing/` |
| `Tests/StartupParams/` | `Tests/App/StartupParams/` |
| `Tests/StepResult/` | `Tests/App/StepResult/` |
| `Tests/TestModule/` | `Tests/Modules/Test/` |
| `Tests/Ui/` | `Tests/Modules/Ui/` |
| `Tests/Variable/` | `Tests/Modules/Variable/` |

## Decisions made

1. **Regrouping happens as part of the move**, not as a later pass. Condition/ 25→4, Error/ keeps its flat per-scenario files (no obvious cluster), Event/ flat (each event hook is a scenario). No speculative regrouping where flat is fine.
2. **`Basic.test.goal` collisions across modules are OK** — path + grep handles it.
3. **`Modules/Builder/`** for all current `Tests/Builder/*` (they are module-action tests). **Top-level `Tests/Builder/`** is reserved for future pipeline-level integration tests; empty at start.
4. **`App/`** (not `Runtime/`) for cross-cutting runtime behavior — matches the c# layer name `PLang/App/`.

## Migration mechanics

1. `git mv` only — preserve history.
2. One commit per top-level module/bucket — reverting one is clean.
3. After each move batch, `plang --build` (rebuilds `.pr` with new paths) then `plang --test` on the moved subset. Green before moving to next bucket.
4. `.build/` directories under each old location regenerate — tied to goal paths.
5. Any `.goal` that `goal.call`s a path literal needs updating. Relative in-subtree calls survive; cross-directory calls may break.
6. Delete `Tests/Runtime2/` outright.

## What this proposal is NOT

- Not a rewrite of any test — pure reorganization (+ minor concept regrouping under Condition/ if approved).
- Not changing `.test.goal` naming convention or PascalCase for callable goals.
- Not changing the lowercase convention for `.fixture.goal` files.
- Not moving any files yet — proposal awaiting approval.
