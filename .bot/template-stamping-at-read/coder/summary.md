# Template stamping at read — summary

**Version:** v1, increment 1 landed.

## What this is
Move template stamping (the authored-vs-literal `%ref%` decision) off the post-parse
`StampedForm` walk and onto the read: **the reader hands the type its template mode
at construction; the type decides.** Trust rides the reader instance, never the
content — so a forged `%secret%` in an http body can't render.

## Increment 1 — born with the mode (landed, green, pushed)
- **`text` ctor**: `bool canTemplate` → `string? template`. `Template = (template !=
  null && HasHoles) ? template : null`. The old `canTemplate:true` hardcoded `"plang"`
  mode-blind — the bug. The type owns the holes-decision, so `HasVariableReference`
  (gates on `Template != null`) stays correct: a holeless string drops the mode.
- **`ReadContext.Template`** carries the mode; **`Wire`** gains a `Template` ctor
  param and feeds it into the `ReadContext` for `ITypeReader.Read`.
- **`_authored`** options on the plang serializer (`template="plang"`) — the single
  trusted construction site. `Deserialize<goal>` routes to it (a goal is the only
  inherently-authored type); `_inbound` (runtime messages) stays mode-off.
- **Trust boundary (traced):** goal deserialization is the only authored read. Path 1
  (`goal.list` → `Deserialize<goal>`) now reads through `_authored`. Path 2 (`GoalCall`
  → catalog options) still seam-stamps for now.
- **Seams stay this round** (`StampTemplates`/`StampedForm`) — idempotent; the read
  stamp + seam stamp coexist. Security already improves: runtime-ingest text no longer
  stamps at the ctor.

### Proof
`dev.sh full` green (counts identical to baseline). Security test
(`TemplateStampOnReadTests`): same `%ref%` bytes → `Template="plang"` under authored
mode, null under runtime mode, null for a holeless string even authored.

### Code example
```csharp
// text.Read — the reader hands the mode; the type decides:
new text.@this(reader.String(), ctx.Template) { Kind = kind }
//   ctx.Template "plang" + has holes  → live template
//   ctx.Template null  (runtime ingest) → literal
//   holeless string                     → literal regardless
```

## Increment 2 — container slots (landed, green, pushed)
`item.serializer.json.ReadSlot` now takes the `ReadContext`; a templated string slot
in an authored container rides as a stamped `text{Template}` item (the "elevated
slot"), a literal slot stays raw, a runtime-ingest slot is always raw. This also
**fixes the live bug** the design flagged (§1.3): the stamp rides the slot, so it
survives the container's fresh-per-read model instead of being re-derived by the
seam's materialize-once hack. `list`/`dict` readers thread `ctx`. Tests: authored
container stamps the `%ref%` slot + leaves the literal; runtime container stamps
neither. Full suite green (Wire 512).
Gap (covered by the still-present seam): a `%ref%` nested inside a *structured* slot
(object/array) goes through `ParseRaw → Parse` which doesn't thread the mode yet —
thread it when removing the seam.

## Mutation finding (2026-06-19) — the seam is far more load-bearing than the doc implied
Disabling `Data.Authored()` (the whole post-parse stamp) → **70+ failures** across every
suite (Modules 13, Types 3, Data 30, Generator 20, Runtime 4, Wire crashed). So
read-stamping (incr 1+2) covers only a NARROW slice — a value read as a **text token**
via the typed reader. The bulk of authored stamping still flows through `StampedForm`:
- **`.pr` params reload as `source`/`clr` carriers**, not as text. `StampedForm`'s
  `source`/`clr` arms (`this.cs:512-532`) collapse a templated carrier to a stamped
  text. Born-with-template doesn't reach these — a deferred/carrier value materialises
  LATER, outside the read-mode context, so the mode must be **captured on the
  source/clr at read and applied at materialise** (new plumbing, not a token stamp).
- **Action-load paths beyond `Deserialize<goal>`** — compile-response rebuild,
  `FromWire`, `GoalCall` (catalog deserialize) — don't run through the `_authored`
  Wire at all, so their params only stamp via the seam.

**Consequence:** deleting `StampedForm` is NOT remaining "cleanup" — it's a substantial
follow-up: born-stamped at the source/clr carrier materialisation (mode captured at
read) + routing every authored-action-load path through the authored read. It couples
with the format-agnostic `ReadData<TReader>` work too. The seam stays as the universal
stamper for now; read-stamping is the first correct slice (the security + live-bug fixes).

## What IS finished and correct (the landed value)
- Born-with-template mechanism: the reader hands the type its mode; the type decides
  (incr 1). Security fix — runtime-ingest `%ref%` never stamps.
- Container slots read-stamp (incr 2). Live-bug fix — the stamp rides the slot.
- Both proven; full suite green WITH the seam in place (the seam + read-stamp coexist
  idempotently).

## Remaining (the larger follow-up — to reach the OBP win of deleting `StampedForm`)
Removing the post-parse walk needs **every** authored read to stamp at read first:
2. **Path 2 (GoalCall)** — route the catalog goal-deserialize through the authored
   Wire so a sub-goal's `%ref%` params read-stamp (today they seam-stamp).
3. **Delete** `StampedForm`/`Authored`/`RawGraphHasRef`/`StampEntry` + the
   `goal.list`/`GoalCall` seams.
4. **`FromWire`** (the risk): rebuilds actions from already-parsed values — confirm
   each caller's upstream read is mode-on before deleting its seam; may keep it.
5. **path** mode-gating (thread the mode to path construction; today hardcodes "plang").

---

## v3 — Migrate PLang.Tests/Data executor tests to the real read path

**What this is.** Same migration pattern as the Modules pass (v2): hand-built
goal/step EXECUTORS in PLang.Tests/Data now load through the real channel read so
params born-type like a `.pr` off disk (`RealGoalLoad.ViaChannel` + `Make.Goal` +
`TestApp.Create`). Copied the landed pattern (ConditionIfBranchIndexTests,
AfterActionPayloadTests, PlangRuntimeTests) — no new design.

**Migrated:**
- `App/Core/EngineTests.cs` — 3 `RunGoalAsync` behavioral tests
  (`RunGoalAsync_ExecutesSteps`, `_WithActor_UsesActorContext`,
  `_ByName_WithActor_UsesActorContext`). `MakeStep*` helpers kept (still used by
  left-alone tests).
- `App/Core/StartGoalTests.cs` — `StartGoal_Programmatic...` + the five
  `ResolveValue_*` tests. `MakeStep`/`MakeStepWithDefaults` helpers + custom
  `CapturingWriteHandler` kept.

**Left alone (with reason):**
- EngineTests: constructor/dispose/actor tests (no goal exec); `steps.RunAsync`
  kernel/step-machinery tests (precedent: PlangRuntimeTests keeps these hand-built);
  `_CancelledToken` (step never runs); `_StepFailure` (deliberately-malformed
  variable.get); `_EmptyGoal` (no steps).
- StartGoalTests: the three `Defaults_*` tests — `Make.Action` has no `Defaults`
  seam (`MakeStepWithDefaults` builds the action's `Defaults` field).
- `PrPipelineTests.cs` — left entirely. FullPipeline/ReadFile/FilePathsFromRoot
  already load real `.pr` fixtures off disk; the hand-built ones
  (SubRelative/ParentTraversal/ParentAndDown) depend on the goal's subfolder `Path`
  (e.g. `/sub/...goal`), which `Make.Goal` hardcodes to `/{name}.goal` (root).
- `GoalTests.cs`, `StepTests.cs`, `VariablesTests.cs` — structural /
  Variables-collection unit tests; no goal execution.

**Read-path gap found.** `Make.Goal(name, ...)` hardcodes `Path = "/{name}.goal"`
(root). Goal-path-dependent tests (relative-resolves-against-goal-folder, parent
traversal) can't migrate without losing intent. If those should ride the read path,
`Make.Goal` needs an optional path/subfolder parameter.

**Proof.** `dotnet build PLang.Tests/Data` clean (only pre-existing CS8981 warnings).
Suite: `failed: 1` = pre-existing `Diff_DiffModeOverLargeListDoesNotOom` (perf/OOM,
unrelated). StartGoalTests 9/9 and EngineTests 35/35 pass. Not committed (per task).

### Code example
```csharp
// before — hand-built nested Goal/Step/PrAction, bypasses the read:
var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal",
    Steps = new GoalSteps { MakeStep("variable","set",
        new Dictionary<string,object?>{{"name","test"},{"value","hello"}}, 0, "set variable") } };
// after — loaded through the real channel read, params born-type:
var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("TestGoal",
    Make.Step("set variable",
        Make.Action("variable","set", Make.Param("Name","test","variable"), ("Value","hello")))));
```
