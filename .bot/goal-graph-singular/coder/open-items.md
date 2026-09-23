# Open items — branch `goal-graph-singular`

Kept here so the list survives a session. Ordered by what I'd do, not by when it was found.
Everything below is on top of a clean, pushed tree (`16bbc24c4`).

## The work itself

**0. Error model redesign — DESIGN SETTLED, ONE BLOCKER, no green light.** Full record in
`error-model-decisions.md` (same folder). Summary: `ErrorChain`→`list` (caused-by), `Error.Action`
added, `Validate` returns `IError?` with causes underneath, no error state on the node, `app.Error`
and the run-wide audit deleted, `trail`/`scope` both gone. **Blocked on:** `context.Error` cannot be
a property — `context.Error(IError)` (the failed-Data factory every handler uses) already owns that
name. Also unresolved: where the recovery scope (`Push`) lives once `app.Error` is gone.

**0b. Ignored errors are silently swallowed.** `error/handle.cs`:
`if (await IgnoreError.ToBooleanAsync()) return context.Ok();` — no push, no log. The empty
try/catch, present today. Emitting them on a redirectable channel was discussed and explicitly
deferred by Ingi ("thinking out loud… swallowed for now").

**1. Finish Stage D — the Validate trilogy.** `action.Validate` is landed but has **no callers**
(an incomplete rung is its own small liability). Remaining: `action.list.Validate`, then point
`build.validate` at it so the builder only *reacts* (re-prompt / abort). `build.validate` receives
an action LIST, not a goal — so `goal.Validate` / `Step.Validate` have no caller yet and were
deliberately not written. Item 3 below changes what `Validate` yields, so do it first.

**2. `action.Requires` — plural bothers Ingi.** It returns the set of things an action reaches
(`network`, `llm`). Every other collection on the graph is a singular concept node (`action.Warning`,
`step.Action`), so the plural is the odd one out. Needs a name that reads singular over a collection.

**3. `action.BuildError` → `action.error` (probably `error.list`).** Ingi: it should be the action's
error node, not a one-off compound. There is already `app.error.list.@this`, and `action.Warning` is
`warning.list` — so `action.Error` as an `error.list` matches the existing shape exactly. It also
makes `Validate` natural: verdicts land on the node instead of being yielded to a caller. **Do this
before Stage D** — it decides `Validate`'s signature.

**4. `Property.Rows` is a middleman.** `Rows => Items.Select(d => d.Clr<Property>()).ToList()` —
proxies what the collection already is, lowers to CLR, and reallocates per read. The fix is NOT
a rename to `.list`: the list holds `Data` wrappers and `Rows` exists to unwrap them, so renaming
just spreads `.Clr<Property>()` into all four callers. Make `property.list` enumerate its own typed
rows (`IEnumerable<Property>`) so it reads `foreach (var row in element.Property)`.

## Risk carried into main

**5 + 6 — CLOSED (`4deaa8921`).** Both were prompt-level and both were real. Read the rendered
prompt from a `cache:false` build: `## Available types` rendered EMPTY always (`%!app.type.list%`
navigates to nothing — `module.list` has a native `list` member, `type.list` has none), dead since
`92f846d09`; and `→ returns item` reached the LLM with `item` defined nowhere in the prompt. Section
and its feeding step deleted (primitives + catalog types already render elsewhere); `item` now
explained once. **Lesson worth keeping: neither was visible from code or tests — only from the bytes
the model receives.**

**5b. The builder's `.pr` hashes are stale.** `goal.Hash` is `SHA256(Name + concat(step.Text))`,
computed AND stored in the `.pr`. In `BuildStep/.build/start.pr` the stored hash disagrees with the
steps for ROOT, `Compile`, `QueryAndVerify`, `RefineActions`, `FixValidation` — **verified stale at
`HEAD~1`, i.e. before my edit**, from earlier hand-edits. `HandleStepFailure` and `EmitSummary` match.
Impact looks nil for building (builds pass), but anything using hash for staleness sees a lie.
Recomputing is a few lines; deliberately NOT done unilaterally on a bootstrap artifact.

## Test-infrastructure problems (these are why the above stayed hidden)

**7. Suite discovery is flaky — the meta-problem.** Totals swing 425–756 across runs of identical
code; whole classes silently don't run. Consequences: every change this stretch had to be verified
by stash+rebuild and a failure-NAME diff, because counts prove nothing; and item 8 sat red all
session without appearing in a single full-suite sample. Until this is fixed, no green result on
this branch means much.

**8. `DiscoverActionTests` is 7/10 red.** Verified identical on the stashed tree — pre-existing, not
from this work. But it includes **both auto-tag tests**, so `action.Requires` (item 2) has no green
coverage.

**9. `PathSerializerMigrationTests` alternates 0-then-2 failures** across consecutive isolated runs
of identical code. Real shared-state race in scheme registration, not caused by this work.

## Process gap

**12. The builder has never built itself on this branch.** Every commit touching
`os/system/builder/*/.build/*.pr` is a hand-edit ("bootstrap builder .pr", "fix param wire shape",
"fix stale visibility"). There is no forcing function that the builder's own goals still compile —
which is exactly how items 5 and 6 survived across many commits, and how five stored hashes drifted.
Ingi (2026-09): worth recording, but we are deep in refactoring and this branch merges up into
another that may be where this belongs — decide when we get there, do not chase it now.

## Small cleanups

**10. `goal.list` late stamp** — `_goals = new goal.list.@this { App = this }`, the same smell removed
from the module registry (which now takes its App at construction). Left alone only because the
injection seam differs.

**11. Stale test** — `ModulesDescribe_BuilderRecordHandlers` NREs looking up module `"builder"`; the
module is `"build"`.

## Unfinished demolition (not live design)

**13. `app/Info.cs`** — DONE (demolished with Data.Warnings; see the commit "Info is gone"). Was: on the branch plan's demolition list ("app/Info.cs, the four List<Info>
properties", replaced by Warning). Its last builder holder (BuildResponse) is gone and it is no longer
registered as a type (only items are indexed). Remaining holders: `Data.Warnings`
(`PLang/app/data/this.Result.cs:62`, copied at `PLang/app/data/this.cs` ×4) and
`PLang/app/module/action/build/code/Default.cs` ×4 (the build's error list and `MergePrData`).

**14. Named survivors of the type registry pass** — executioner: the *type entities born with
context* pass (shape with Ingi: item.Type(context) / items carry context / static data table / the
entity never reaches the registry from inside itself). Until then these statics stay, by name:
`type.list.@this.GetPrimitiveOrMime` (the context-less `ClrType` fallback, `type/this.cs:163`),
`type.list.@this.ClrFromMime`, and the static `app.type.primitive.@this` table (read by the entity's
context-less constructor: `Canonicalise`, `StampPrimitive`). `Get(name)` / `Clr(name)` die with them
(`variable/set.cs:217`'s `type.ClrType ?? Type.Get(name)` fallback included).

**16. Two descriptors for "a named, typed slot"** (for the type-entity pass) — `goal.step.action.property.@this`
(an action's parameters: Name, Type entity, Nullable, Default, IsVariable) and `app.type.Field` (a record
type's fields in the catalog fold: Name, `TypeName` — a string, the flat copy again). An action's
parameters and a record's fields are the same concept; when the entity describes itself, a record's
fields are read from its declaration by the same reflection `property.list` already does. Likely one type.

**15. Action return type is a flat copy** — `action.Return` / `ReturnTypeName` is a string read off
the entity's face (`goal/step/action/this.Schema.cs:83`), stored beside the type it names. It becomes
the type entity; the catalog renders its face.

**17. Runtime presence checks → `App.Mode`** — the App's `Mode` (run | build | test) is derived from
`Build` / `Test` presence and is what the snapshot captures. The runtime still branches on the presence
itself (`app/this.cs:516` `if (Build != null) return await Build.RunAsync();`, `:547` `if (Test != null)`).
Reading `Mode` there instead is its own later item — not part of the restore pass.

## Snapshot — parked by Ingi

**18. Snapshot restore — parked mid-pass.** Landed and kept (six suites green by name): `be8a30fc5`
(ISnapshot is {Section; Capture; Task Restore}, all instance; App walks one owner list
[Code, Variable, Statics, App, CallStack]; App's derived `Mode` replaces the build/test presence bits),
`64cca5d21` (guarded frame reads; actionModule/actionName verified — CallbackActionMismatch),
`8a8bd8c01` (each captured variable its own entry; snapshot.Get/Set delegate to its Entries),
`6aeba1e70` (docs). Plan: `restore-remainder-plan.md`.

Six SnapshotWire reds stay red as parked: `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`,
`MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`, `PlangPath_AsSnapshotConvert_EditSurvivesResume`,
`ThrowTimeSnapshot_EditSurvivesResume`, `TypedSnapshotString_NavigateEditResume_PersistsEdit`,
`NavigateAndEditCapturedVariable_ThenResumeToSuccess`.

Trace (start here): **the wire read works** — `App.SnapshotFromWire(json)` returns all five sections, and
Variables holds each captured variable as its own entry. Every red instead converts a plain JSON *string*
into a snapshot through a door the design does not have:
- `snapshot.Create(text(json))` — Create is a pass-through courier → declines;
- untyped `new Data("", json).Value<snapshot>()` → "holds a text — 'snapshot' cannot be created from it";
- `Type["snapshot"].Create(json)` then Value → the content door reads raw text through value.Reader,
  "scalar-only — a structured value needs a format parser".

Open question: does a text holding the plang wire convert into a snapshot?
(a) No — a snapshot comes back only through the wire door (SnapshotFromWire / a wire-typed Data); the six
tests switch to it and keep their navigate/edit/resume bodies; plang-side `%json% as snapshot` is not a feature.
(b) Yes — and the one-door way is general, not a snapshot special case: a content source of a STRUCTURED type
reads its raw text through the transport format's parser. The `resume` verb's doc
(`module/action/snapshot/resume.cs:6-12`, "Create reads the wire through the plang serializer") is stale
either way and waits on this.
