# For architect — goal `.pr` reads via TWO materialization paths; they disagree after the item-drop

**From:** coder. **2026-07-09.** Blocking the last 4 Data regressions from the item/ICreate drop.
Ingi flagged: "make sure to select correct path of reading of goal."

## Symptom

`app.Goal.GetAsync("NormalGoal")` returns **null** for a `.pr` that exists on disk
(`GetAsync_ReturnsGoal_ForNonSetupGoalLoadedFromDisk`). `RunGoalAsync_ExecutesSteps` (a
different load entry) works. Same file, same JSON — different result.

## Root cause — two paths build a goal from `.pr` JSON, and only one was migrated

The item-drop made `goal` a plain host carried as `clr<goal>`. Every *consumer* was updated to
unwrap `((await x.Value()) as clr<goal>)?.Value`. But there are **two producers**, and only the
reader was migrated:

```
PATH 1 — the READER  (yields clr<goal>  ✅)
  data.Value() → source.Value → Text.Read (channel/serializer/Text.cs:84)
    → Readers.Reader("goal","*") → goal.serializer.Reader   (I fixed this: opens a json.Reader
      over reader.RawValue(), drives reflection.Read → clr<goal>)
  USED BY: RealGoalLoad.ViaChannel / RunGoalAsync.

PATH 2 — ReadText EAGER-CONVERT  (yields raw goal  ❌)
  prPath.ReadText()                                   // type/path/file/this.Operations.cs:113-119
    mime = Format.Mime(".pr") = "application/plang-goal"
    materialized = catalog.ClrFromMime(mime) = typeof(app.goal.@this)
    content = App.Type.Convert(text, typeof(goal), ctx).Peek()   // catalog/Conversion.cs:74
              → [Obsolete] TryConvert → a BARE Goal (STJ-ish), never wrapped as clr<goal>
    return new data(Raw, content /*raw Goal*/, type"goal", ctx)
  USED BY: goal.list.LoadFromFileAsync:368, GetAsync:188/214, GoalCall.LoadFromFile:309.
  Those all now do `as clr<goal>` → null → "Failed to parse goal file" → GetAsync returns null.
```

So the same `.pr` yields `clr<goal>` through the reader and a bare `Goal` through `ReadText`.
`ReadText`'s consumers were migrated to the clr shape; `ReadText` itself was not.

## Why I didn't just patch it

`ReadText`'s eager-materialize branch (`this.Operations.cs:113`,
`if (materialized != null && materialized != typeof(string))`) fires for **every** non-string
type — `application/json → object`, `.pr → goal`, snapshot reads (line 77), etc. It uses the
`[Obsolete]` `Type.Convert`/`TryConvert` ("Superseded by Type.Create"). Redirecting `.pr` (or all
of it) to the reader path is a **routing decision across the whole read perimeter**, not a local
edit — it's Stage 1c ("data/reader routing"), and it touches how EVERY typed file read
materializes. That's yours to call, not mine to quietly change.

## The question

**Should `ReadText` stop eager-Converting host types and instead produce a source-backed value
that materializes through the ONE reader (`goal.serializer.Reader` → `clr<goal>`) on `.Value()`?**

My read (A + reasoning, for you to confirm/redirect):

- **A — defer to the reader.** `ReadText` for a `.pr` returns `new data(Raw, text, type"goal")`
  *un-converted*; `.Value()` runs the reader (Path 1) → `clr<goal>`. One producer, one shape.
  Kills the `[Obsolete] TryConvert` dependency at this seam. Risk: the eager branch also serves
  `application/json → object` and the build snapshot path (line 77) — do those want the same
  deferral, or only host types (goal)? If only goal, the branch grows a "is this a reader-owned
  host type?" fork, which smells.
- **B — wrap at Convert.** Make `App.Type.Convert(text, typeof(goal))` return `clr<goal>` (a host
  materializes as its carrier). Localizes the change to conversion, leaves `ReadText` alone. But
  it keeps TWO producers alive (reader + Convert) doing the same job — the divergence can recur.

I lean **A** (one producer) but the json/snapshot blast radius is the open question. Once you pick,
I'll land it + the 3 other item-drop tails below.

## The other 3 item-drop tails (I can handle; noting for completeness)

1. `Set_GoalAsDataSubclass_StoredDirectly`, `Set_GoalStepsBracketIndex_PreservesGoalIdentity` —
   assert a variable holding a goal returns a **raw `Goal`** (`ReferenceEquals(Peek(), goal)`,
   `IsTypeOf<goal>`). Under the confirmed **Peek→self** clr model it returns `clr<goal>`. These are
   old-model test assertions — I'll update them to `.Clr<goal>()` (test-authoring fix, confirmed model).
2. `Get_GoalGoalsCount_ReturnsCount` — `goal.Goals.Count` navigation returns uninitialized. The
   list kind's `Descend` handles integer index only; `.Count` (a named member on a CLR list host)
   isn't navigable. **Semantics question:** does the list kind fall through to reflection for named
   members like `.Count`, or is `.Count`-on-a-list-host navigation dropped? (Small, but a model call.)

## What I already landed this session (compiles clean, 0 errors)

- Declared-face rule in `reflection.Output` (your `stage1-output-contract-answer.md`): untagged
  plang-assembly type at Output → loud `NoWireContract`; untagged foreign → transparent dump.
- list kind claims `IEnumerable` (was `IList`); the `Kind[clrType]` door picks the **most-derived**
  assignable claim (`IDictionary` → dict beats `IEnumerable` → list), `string` excluded.
- `Discovered.TypeForName/TypeForClr` → indexers `_shared[name]` / `_shared[clrType]` (Ingi: the
  verb+noun public name was a smell; a keyed lookup is an indexer).
- Path 1 fix: `goal.serializer.Reader` opens a json reader over the raw bytes (was handed a
  scalar `value.Reader` → `BeginObject` threw).
- `GoalCall.GetGoalAsync` in-memory hits now return `clr<goal>` (a local `Found(goal)` helper),
  matching the .pr-load shape — fixed the `RunGoalAsync` NRE cluster (~19 Data tests).
