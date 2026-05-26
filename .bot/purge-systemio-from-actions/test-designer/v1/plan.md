# Test plan — purge `System.IO` from PLang

**Version:** v1
**Author:** test-designer
**Date:** 2026-05-25
**Architect input:** `.bot/purge-systemio-from-actions/architect/plan.md` (13 design decisions, 7 stages)

## Reading of the architect's plan

This is **security work**. The type flip (`string → path`) is the mechanism; the **denial paths** are the value. Today's `.test.goal` suite is mostly happy-path — most handlers can touch the OS wherever the process can reach, because `System.IO` doesn't care about roots. The branch's promise is that every filesystem reach routes through `AuthGate`. The tests that prove this work are the ones that don't yet exist.

I weight my batches accordingly. Derivation-verb correctness gets one focused batch (D1 is pure functions, easy). Goal/Path round-trips through JSON get one batch (D3 + C7/C11 — high-risk, atomic flip). Everything else is **denial-path coverage**: out-of-root reaches that today silently succeed and post-migration must be gated, prompted, or denied.

I diverge from the architect's stage list in one way: I treat D13 (the `.Absolute` discipline rule) as test-shaped, not doc-shaped. The rule is only real if a test catches a missing `Authorize` before `.Absolute`. That gets its own batch with a mutation-test pattern, even though the architect lists it under Stage 7 (docs).

## Batch index (high-level)

| # | Batch | Stage | Lang | Approx count |
|---|---|---|---|---|
| 1 | Path derivation verbs (D1)                                   | 1 | C#    | 10 |
| 2 | PLNG002 analyzer (D12)                                       | 1/6 | C#  | 8  |
| 3 | `.goal` MIME → Goal deserialization (D2)                     | 2 | C#    | 6  |
| 4 | Goal/GoalCall typing + JSON converter (D3, C7/C11)           | 3 | C#    | 9  |
| 5 | AppGoals + App.Load/Save migration (D4, D5, D6)              | 4 | C#    | 8  |
| 6 | Execute verb + `LoadAssemblyAsync` (D8)                      | 5 | C#    | 7  |
| 7 | Content-shape verbs `ReadAsBase64` / `ReadAsDataUri` (D9a)   | 5 | C#    | 6  |
| 8 | `.Absolute` discipline + mutation-test fixture (D13)         | 5 | C#    | 5  |
| 9 | Handler denial paths (Ring 2: discover, http, Fluid, sqlite, OpenAI, debug) | 5 | C# + PLang | 12 + 8 |
| 10| In-root silent fast-path regression guard                    | 5 | C#    | 4  |
| 11| Path equality / dict-keying under `RootComparison`           | 3/4 | C#  | 5  |

**Total:** ~80 C# tests + ~8 PLang `.test.goal` tests.

## Test-style anchors

- **C#:** TUnit `[Test]`, `async Task`, bodies are `Assert.Fail("Not implemented");`. Reuse `CannedChannel` / `StatelessChannel` from `PLang.Tests/App/FileSystem/SurfaceTests/FileSystemPermissionFlowTests.cs` for actor/channel setup.
- **PLang:** one goal per `.test.goal`, name starts with `Test`, second line is the spec comment, body is `- throw "not implemented"`. Lives under `Tests/Path/` (new folder) and `Tests/Permission/` where it fits.
- **Out-of-root fixture pattern** is the architect's open question (D9, section "Test infrastructure"). I'll specify the fixture shape as part of batch 8 — a `RootedActorFixture` helper that takes a root, a verb-answer script (`"y" | "n" | "a"`), and exposes the resulting `app.@this` + audit-trail of `Ask` invocations.

## Folder placement

- Derivation verbs → `PLang.Tests/App/Types/PathTests/DerivationTests/`
- PLNG002 → `PLang.Generators.Tests/Plng002AnalyzerTests/` (new project if it doesn't exist; if it does, mirror existing layout)
- `.goal` MIME → `PLang.Tests/App/Types/PathTests/MimeDeserializationTests.cs`
- Goal/GoalCall typing → `PLang.Tests/App/Goals/PathTypingTests/`
- AppGoals + App.Load/Save → `PLang.Tests/App/Goals/AppGoalsMigrationTests/`, `PLang.Tests/App/AppLoadSaveTests/`
- Execute verb + DLL → `PLang.Tests/App/FileSystem/PermissionTests/ExecuteVerbTests/`, `PLang.Tests/App/Types/PathTests/LoadAssemblyTests.cs`
- Content-shape verbs → `PLang.Tests/App/Types/PathTests/ContentShapeVerbTests.cs`
- `.Absolute` discipline → `PLang.Tests/App/FileSystem/SurfaceTests/AbsoluteDisciplineTests.cs`
- Handler denial paths (C#) → `PLang.Tests/App/Modules/<module>/DenialPathTests.cs`
- Handler denial paths (PLang) → `Tests/Permission/<scenario>.test.goal`
- Silent fast-path → `PLang.Tests/App/FileSystem/SurfaceTests/InRootSilentTests.cs`
- Equality/keying → `PLang.Tests/App/Types/PathTests/RootComparisonTests.cs`

## What I'm explicitly NOT testing

- Type flips themselves (build errors catch them; no behavioural test adds value).
- Pure renames (compile-time work).
- Docs (Stage 7).
- HTTP scheme for HTTP requests (architect noted out-of-scope).
- `App.AbsolutePath` / `App.OsDirectory` (D7 — perimeter stays string).
- Re-running existing `.test.goal` suite (those exist; merge gate is "they stay green").

## Workflow

1. Present batch 1, wait for approval.
2. Repeat for batches 2–11, incorporating feedback each time.
3. After all batches approved, write the test files, commit, push.
4. Write `verdict.json: { "pass": true }`.
5. Update `summary.md`.
6. Hand off to coder.
