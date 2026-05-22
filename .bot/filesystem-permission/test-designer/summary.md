# Test designer — filesystem-permission

## Version
v1

## What this is
PLang's filesystem permission system: signed per-actor grants on paths with
read/write/delete verbs and sub-options, gated through a snapshot/resume
machinery that lets the runtime suspend mid-goal when consent is needed and
resume after the user answers. This is the test contract — ~70 C# TUnit
stubs and 11 PLang test goal stubs whose bodies are `Assert.Fail("Not
implemented")` / `throw "not implemented"`. The tests *are* the spec; the
coder makes them pass.

## What was done

Read the architect's 5-stage design, wrote the v1 plan (`v1/plan.md`) with
a 10-batch breakdown, and — per Ingi's blanket approval — wrote every batch
in one pass instead of walking through batch-by-batch.

Files created:

- C# tests (16 files under `PLang.Tests/`):
  - Stage 1: `App/FileSystem/PermissionTests/VerbCoversTests.cs`,
    `PermissionCoversTests.cs`
  - Stage 2a: 8 files under `App/CallbackTests/` — `TypeExitTests`,
    `ActionSyntheticTests`, `DataSnapshotTests`, `StepLoopShouldExitTests`,
    `GoalRunFromTests`, `ActionRunAsyncTests`, `OutputAskRoutingTests`,
    `SnapshotResumeTests`
  - Stage 2b: `App/FileSystem/PermissionTests/AuthorizeTests/PathAuthorizeTests.cs`
  - Stage 3: `App/FileSystem/PermissionTests/StorageTests/ActorPermissionStorageTests.cs`
  - Stage 4: 3 files under `App/FileSystem/SurfaceTests/` — `FileSystemPermissionFlowTests`
    (parametrized), `FileSystemSurfaceShapeTests`, `MoveCopyBundledConsentTests`

- PLang test goals (11 files under `Tests/`):
  - Stage 2a: `Callback/StatefulAskMidGoalBindsValue/Start.test.goal`,
    `Callback/StatelessCrossGoalResumes/Start.test.goal` +
    `AskAQuestion.goal` (sibling sub-goal)
  - Stage 2b: `Permission/Authorize/Stateful…/Start.test.goal`,
    `Permission/Authorize/Stateless…/Start.test.goal`
  - Stage 5: six `Tests/Permission/{NoGrantSuspends,GrantAStoresPersisted,
    ImmediateRereadSkipsPrompt,RestartStillNoPrompt,RevokeReprompts,
    NarrowedGrantRejectsWiderRequest}/Start.test.goal`

Test layout follows the `*Tests` suffix convention to dodge the
`Data` / `Variables` alias clash in `PLang.Tests/GlobalUsings.cs`.

Coder follow-ups (flagged in `v1/plan.md`):
1. Some Batch 6 "stateful e2e" tests may need real Stage 3 storage rather
   than mocks — coder decides while sizing.
2. Stage 4 parametrization uses `[MethodDataSource]` over a method-name
   enumerable; if TUnit prefers a different shape here, switch.
3. The static-survey "no `App.Run` / `App.RunAction`" tests need a Roslyn-
   over-`PLang/` helper; if no such pattern exists in the suite, propose
   the helper before adding more like it.

## Code example

C# stub:

```csharp
[Test] public Task Authorize_StatefulAnswerA_SignsWithAlwaysExpiry_Adds_ReturnsOk()
    { Assert.Fail("Not implemented"); return Task.CompletedTask; }
```

PLang stub:

```
Start
/ Stage 5 (2/6) — Driver answers "a" on the suspended Snapshot. On resume,
/ file.read re-runs; Authorize signs with AlwaysExpiry; user.Permission.Add
/ persists. The `permission` table holds the signed row.
- throw "not implemented"
```
