# C# Test Consolidation Plan — "test through the PR, not around it"

## Thesis (Ingi)
PLang is a programming language. Everyone interacts with it two ways: **(1) the PLang
language (PR structure)** or **(2) a new C# module**. So a test should build a `.pr`
deterministically and run it through the runtime. Only a thin floor — things with no
language surface — stays as C# unit. Target: remove ~90% of the ~4,099 test methods.

## Current state
- ~503 test files, **~4,099 `[Test]` methods** (Types 720 / Modules 973 / Data 938 /
  Runtime 769 / Wire 515 / Generator 184 / Shared 0).
- ~half the files hand-construct internal C# objects (`new Set{...}.Run()`,
  `TestAction.RunAsync`, `Data`/`Type` API pokes) — the wrong altitude.
- The bulk is **enumeration of a few dozen mechanisms at the C# API level**, not
  distinct behaviors (e.g. `TypeMappingTests` = a mapping table unrolled into 90 methods;
  `DataTests` 96, `EngineTypesTests` 88).

## The replacement shape (already exists, deterministic — NOT the LLM builder)
```
Make.Goal(...)            // build PR by hand, born-typed like the runtime
  → RealGoalLoad.ViaChannel // serialize → stream channel → the real .pr read boundary
  → engine.RunGoalAsync     // dispatch through the engine
  → assert on output channel / variable state / returned Data / raised error
```
Each goal-run test proves the **full stack** a unit test skipped: born-typing, `%var%`
resolution, `Data<T>` wiring, source-gen guards, dispatch.

## The floor that stays C# unit (no language surface)
1. **Source generator (184)** — compile-time codegen.
2. **Thin mechanism tier (~15–30 files)** — lazy-deserialize "did only touched branches
   materialize", wire byte-determinism/ordering, a couple of cache/timing probes.
3. **Build-time validation** (`ValidateBuild` / `IBuildValidatable`) — runs at build,
   `Make.Goal` bypasses it. Either keep as a thin unit or build a deterministic
   build-validation harness.

---

## How we detect a missed path (the core guarantee)

**Coverage-diff gate.** Goal-run tests execute the same C# the unit tests did, so one
collector measures both. For every module:

1. Run the **existing unit tests** with coverage → record covered `(file, line)` set = *baseline*.
2. Write goal-run replacements.
3. Run **only the goal-run tests** with coverage → record covered set.
4. **Diff.** Any line in `baseline − goalrun` is a path the new suite drops. Each lost
   line gets one of two dispositions:
   - **Convertible** → add a goal-run case that reaches it.
   - **Floor** → keep a named C# unit (build-time / no-language-surface).
5. A module is "done" only when `baseline − goalrun ⊆ {explicitly-kept floor lines}`.

Command (TUnit / Microsoft.Testing.Platform):
```
dotnet exec PLang.Tests.Modules.dll --treenode-filter "/*/*/<Class>/*" \
  --coverage --coverage-output-format cobertura --coverage-output <name>.cobertura.xml
```
No line-rate hand-waving: the gate is the **line-set difference**, not a percentage.

---

## Pilot result — `variable` module (done, measured)

**Before:** 34 unit methods / 6 files (`settests` 10, `SetTypeInferenceTests` 16,
get/remove/clear/exists 2 each).
**After:** `VariableGoalRunTests.cs` — 17 goal-run test instances (~13 methods; the
type-inference matrix collapsed to one `[Arguments]` test of 5 cases). All green.

**Coverage diff (goal-run vs unit baseline), per handler:**

| handler   | unit lines | goal-run lines | missed by goal-run |
|-----------|-----------|----------------|--------------------|
| clear.cs  | 4         | 4              | — (full parity)    |
| exists.cs | 3         | 3              | —                  |
| get.cs    | 3         | 3              | —                  |
| remove.cs | 4         | 4              | —                  |
| set.cs    | 102       | 75             | **27 lines**       |

The 27 missed lines, named exactly by the gate:
- **set.cs 20–69 = `ValidateBuild`** — build-time sync validation. `Make.Goal` bypasses
  the build. → **Floor**: keep the 3 `ValidateBuild_*` as thin C# units (or a build-validation harness).
- **set.cs 205–211 = kind-derivation branch in `Run`** — reachable; the pilot's
  forced-type test only hit the *failure* path. → **Convertible**: add a forced-type
  *success* goal-run (e.g. `set %n% = 42 (string)`), closes it.

**Takeaway:** runtime behavior collapsed cleanly to goal-run with line-for-line parity on
4/5 handlers; the only genuine residue is one build-time method (floor) + one missing
goal-run case (mechanical). The detector named both precisely — nothing silently dropped.

---

## Two standardizations to fold into the sweep

1. **Parameterize type matrices.** A test that only varies the input type
   (string/int/long/decimal/datetime → "number"/"text"/…) is *one* data-driven test, not N.
   `[Arguments("hello","text")] [Arguments(42,"number")] …` on a single
   `Set_InfersType(object value, string expected)`. (Ingi: "send in the type you create
   before calling it" — the value carries its born type; the test asserts the derived
   type name.) `SetTypeInferenceTests` 16 → ~3.
2. **One app factory: `TestApp.Create("/app")`.** 179 files hand-roll
   `new global::app.@this(...)`; only 27 use `TestApp.Create`. Hand-rolled instances skip
   `app.Tester.IsEnabled = true` (in-memory settings — no on-disk pollution), a latent
   flake source. Add `TestApp.Create("/app", settings)` overloads *only as needed*;
   default everything to the factory.

---

## Rollout (module by module, gated)
1. Per module: capture baseline coverage set → write goal-run replacements → diff →
   resolve every lost line (convert or declare floor) → delete the redundant unit file(s).
2. Land the floor inventory in one place (`Generator/`, build-validation harness,
   mechanism tier) so "what's intentionally C#-unit" is explicit, not accidental.
3. Sequence: `variable` (pilot, done) → `list` / `condition` / `output` (high method
   counts, clear language surface) → `Data` / `Types` enumeration files (biggest collapse:
   TypeMapping/DataTests/EngineTypes) → Wire (keep byte-determinism floor) → stop at Generator.

## Projected end state
~4,099 methods → **~700–1,000**, with *higher* real coverage (today nothing tests the
enumeration through the author's path). The deleted ~3,500 are redundant enumeration, not
behaviors.
