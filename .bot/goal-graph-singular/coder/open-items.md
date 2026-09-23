# Open items — branch `goal-graph-singular`

THE live list (the architect references this one; no parallel list). Open first, with current evidence;
Done at the bottom, one line each with its landing commit. Verified 2026-09-23 at `17e4737b0`.

## Open

**5b. The live builder `.pr` hashes are stale — still true.** `goal.Hash` = SHA256(Name + concat(step.Text)),
stored in the `.pr`. Recomputed 2026-09-23 over `os/system/builder/**/.build/*.pr`: **3 of 6 goals stale** —
`Build` (`.build/build.pr`), `Start` and `HandleBuildFailure` (`BuildGoal/.build/start.pr`). The decider-harness
rebuilds write to `tools/decider/out/`, never over the live files, so they did not refresh these. Anything using
the hash for staleness sees a lie. Not recomputed unilaterally on a bootstrap artifact (ties to #12).

**24. `test.Create(goal, context)` is a static async factory** — the no-statics rule catches it (only the
C#-mandated `ICreate` statics are exempt). OBP home: the test collection's lifecycle mints its elements
(`app.Test` asked by discover, as the module list mints module elements). Decide with the births pass.

**25. Births pass — inventory done** (`births-inventory.md` + `.tsv`, at `22e6dafd6`): readers 68, typed ask 39,
pure core 4 external, implicit-in 237, direct `new` 526 (errors 270), raw result doors 150; 34 `Type => new(...)`
getters; `new app.type` outside the registry at 7 sites.

**9. `PathSerializerMigrationTests` / `KindViaCreateTests` path-kind flake.** Not reproduced in 6 isolated runs +
3 full sweeps; the helpers now print the decline's key + message (`a5cf9e221`), so the next occurrence names its
cause. Suspected shared-state race; not quarantined (no named cause yet).

**12. The builder has never built itself on this branch.** Every commit touching
`os/system/builder/*/.build/*.pr` is a hand-edit; nothing forces the builder's own goals to still compile — how
#5b drifted. Ingi: decide on the parent branch, do not chase now.

**14. Named survivors of the type registry pass** — executioner: the *type entities born with context* pass
(shape with Ingi). Until then these statics stay, by name: `type.list.@this.GetPrimitiveOrMime` (the
context-less `ClrType` fallback, `type/this.cs:163`), `type.list.@this.ClrFromMime`, and the static
`app.type.primitive.@this` table (read by the entity's context-less constructor: `Canonicalise`,
`StampPrimitive`). `Get(name)` / `Clr(name)` die with them (`variable/set.cs:217`'s `type.ClrType ?? Type.Get(name)`
fallback included).

**15. Action return type is a flat copy** — `action.Return` / `ReturnTypeName` is a string read off the entity's
face (`goal/step/action/this.Schema.cs:83`), stored beside the type it names. It becomes the type entity; the
catalog renders its face.

**16. Two descriptors for "a named, typed slot"** (for the type-entity pass) — `goal.step.action.property.@this`
(Name, Type entity, Nullable, Default, IsVariable) and `app.type.Field` (Name, `TypeName` — a string). Same
concept; when the entity describes itself, a record's fields are read by the same reflection `property.list`
does. Likely one type.

**17. Runtime presence checks → `App.Mode`** — the runtime still branches on presence (`app/this.cs:516`
`if (Build != null)`, `:547` `if (Test != null)`); reading `Mode` there is its own later item.

**18. Snapshot restore — PARKED by Ingi.** Landed and kept: `be8a30fc5` (owner-named sections, one owner list,
`App.Mode` replaces presence bits), `64cca5d21` (guarded frame reads; action verified), `8a8bd8c01` (each captured
variable its own entry; snapshot navigates its entries), `6aeba1e70` (docs). Plan: `restore-remainder-plan.md`.
Six SnapshotWire reds stay red: `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`,
`MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`, `PlangPath_AsSnapshotConvert_EditSurvivesResume`,
`ThrowTimeSnapshot_EditSurvivesResume`, `TypedSnapshotString_NavigateEditResume_PersistsEdit`,
`NavigateAndEditCapturedVariable_ThenResumeToSuccess`. Trace: **the wire read works** (`App.SnapshotFromWire`
returns all five sections, each variable its own entry); every red converts a plain JSON *string* into a snapshot
through a door the design lacks (`snapshot.Create(text)` declines; untyped `Data(json).Value<snapshot>()` "holds a
text"; `Type["snapshot"].Create(json)` reads raw text through the scalar-only value reader). Open question: does a
text holding the plang wire convert into a snapshot — (a) no, wire door only; (b) yes, generally: a content source
of a STRUCTURED type reads its raw text through the transport format's parser. `resume.cs:6-12` doc waits on it.

**19. Three item types name `clr`** (they declare no name — not @this, no [PlangType]): with Ingi.
- `app.module.action.list.type.list` — returned by 10 list actions; names itself "list" beside the native list, a
  {count, value} wrapper around what the native list already carries.
- `app.module.action.setting.type+setting` — returned by setting.set / remove; a static class used as a namespace
  around a {key, value} record.
- `app.module.action.signing.sign` — the SignOptions slot on http.request / download / upload; an action handler
  doubling as a value type.

**20. Actor literal becomes a choice** — the actor set as a named-set choice (resolves to the live actor at use);
`actor.Convert` dies; slots goal.call / event.on / channel.set / channel.remove / environment.run. After the births
pass (needs the one naming door + births).

**21. `plang --test` loud readers + zero-discovered guard** — a test run that discovers nothing, or whose graph
readers fail quietly, must fail loud. Needs a running builder.

**22. Builder-flow change** — action descriptions into stage 2, the menu holding actions (not "module.action"
strings), notes/examples in stage 3. With Ingi; needs a running builder.

**23. Security regressions — PARKED by Ingi.** Tamper pair (production regression in `da067599c..3e87c6d3b`, real
signing) and masking (`da067599c^..3e87c6d3b`); the test signing mock verifies everything (flip at `6071d0f13`).
Everything to resume is in `security-bisect.md` (candidate lists, driver, oracle `REAL_SIGNING=1`, probes done,
open rulings: blast-radius list, honest mock).

## Done

- **8 Tag rework** — goal owns its tags (`.pr` wire), `test.tag` Build stamps `%goal%`, `IClass.Callee`,
  `test.Create`, `app.Test.Exclusion`, discover collapsed; DiscoverActionTests 10/10: `22e6dafd6`.
- **0b Ignored errors** — marked Handled, stay in the audit, one line under --debug: `10829ad4f`.
- **List writer bug** — only a chunk dissolves; list-valued parameters write as one row: `143b5ad9d`.
- **GoalPathTypingTests** — round-trip through the goal's own writer/reader: `cf0fbe647`.
- **0 Error model** — frame owns recording, `CallStack.Error`, `app.Error` deleted: `f74f484cc`, `a4108b2c2`, `54ab44cd5`.
- **1 Stage D** — `action.Validate` / `action.list` / `step` / `goal.Validate`, `build.validate step=`: `804062686`, `61fc439ea`.
- **2 `action.Requires` → `Requirement`** (Stage C): `a4108b2c2`.
- **3 BuildError** — superseded by the error model; dissolved into `Validate` / `Parse`: `5538fff00`.
- **4 `Property.Rows` middleman** — rows stay hosts; `property.list` a read-only list, Rows dies: `6dd927f44`.
- **5 + 6 builder prompt holes** (empty `## Available types`, undefined `item`): `4deaa8921`.
- **7 Suite discovery flaky** — two identical full sweeps at `17e4737b0` gave identical totals in all six suites
  (963 / 718 / 466 / 884 / 191 / 675); the six-suite split + the whole-suite timeout ended the swing.
- **10 `goal.list` late stamp** — born with its App: `b716f4de6` (+ `Load` rename `8b7379614`).
- **11 `ModulesDescribe_BuilderRecordHandlers`** — repointed to `Module["build"]["goals"]` when `Describe` died
  (`09f3bdc74` test repair); green.
- **13 `app/Info.cs`** — demolished with `Data.Warnings`: `35cb1d1e2`.
