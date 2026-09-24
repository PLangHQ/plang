# Open items — branch `goal-graph-singular`

THE live list (the architect references this one; no parallel list). Open first, with current evidence;
Done at the bottom, one line each with its landing commit. Verified 2026-09-23 at `17e4737b0`.

## Open

**5b. The live builder `.pr` hashes are stale — still true.** `goal.Hash` = SHA256(Name + concat(step.Text)),
stored in the `.pr`. Recomputed 2026-09-23 over `os/system/builder/**/.build/*.pr`: **3 of 6 goals stale** —
`Build` (`.build/build.pr`), `Start` and `HandleBuildFailure` (`BuildGoal/.build/start.pr`). The decider-harness
rebuilds write to `tools/decider/out/`, never over the live files, so they did not refresh these. Anything using
the hash for staleness sees a lie. Not recomputed unilaterally on a bootstrap artifact (ties to #12).

**25. Births pass — inventory done** (`births-inventory.md` + `.tsv`, at `22e6dafd6`): readers 68, typed ask 39,
pure core 4 external, implicit-in 237, direct `new` 526 (errors 270), raw result doors 150; 34 `Type => new(...)`
getters; `new app.type` outside the registry at 7 sites.

**9. `PathSerializerMigrationTests` / `KindViaCreateTests` path-kind flake.** Not reproduced in 6 isolated runs +
3 full sweeps; the helpers now print the decline's key + message (`a5cf9e221`), so the next occurrence names its
cause. Suspected shared-state race; not quarantined (no named cause yet).

**12. The builder has never built itself on this branch.** Every commit touching
`os/system/builder/*/.build/*.pr` is a hand-edit; nothing forces the builder's own goals to still compile — how
#5b drifted. Ingi: decide on the parent branch, do not chase now.

**16. Two descriptors for "a named, typed slot" — IN PROGRESS (Ingi's go).** `app.type.Field` (Name +
`TypeName` string) goes; `property` moves to `type/property/this.cs` and serves both a type's and an action's
properties; the type's `Fields` + `Properties` merge into one `Property` list of `property` entries carrying
type objects. The builder (`type/list/this.cs`) builds `property` entries; the view/templates render the face.

**17. Runtime presence checks → `App.Mode` — queued (Ingi's go).** `Mode` moves out of the snapshot partial
(`app/this.Snapshot.cs`) into `app/this.cs`; `if (Build != null)` / `if (Test != null)` and any other presence
checks read `Mode`.

**30. Error stores an App — queued (Ingi's go).** `error/Error.cs:102` (set by `Errors.Push`, used by
`Callback` at :123). Remove it; the error reaches its App through its own Context. An error with no context has
no callback — a named error, not an NRE. Snapshot is parked: keep that part minimal.

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

**28. Same-actor concurrent `list.add` races** — 25 parallel runs of one goal under the SAME actor, each
`add %x% to %l%`, throw a NullReferenceException inside list.add (the shared %l% list and its creation are not
guarded). Cross-actor concurrency is fine (each actor has its own memory); within one actor, concurrent runs of
the same goal share variables unguarded. Found writing `SharedProgramTests.OneGoal_RunConcurrently…`, which runs
System+User in parallel and never one actor twice at once.

**29. `file://` not stripped** — a `.pr` path literal `file:///tmp/x` resolves to `<root>/file:/tmp/x`:
`Scheme.From` / `FilePath` keep the scheme prefix as part of a root-relative path. Tests use a relative
out-of-root path instead.

**26. Plugin loader registers closed sets as types** — `type/list/Loader.cs:106-125` (runtime-loaded DLLs)
registers every `[PlangType]`, enums included, with no item check; `Registry.cs:172` skips non-items. A plugin's
closed set would land as a type there.

**21. `plang --test` loud readers + zero-discovered guard** — a test run that discovers nothing, or whose graph
readers fail quietly, must fail loud. Needs a running builder.

**22. Builder-flow change** — action descriptions into stage 2, the menu holding actions (not "module.action"
strings), notes/examples in stage 3. With Ingi; needs a running builder.

**23. Security regressions — PARKED by Ingi.** Tamper pair (production regression in `da067599c..3e87c6d3b`, real
signing) and masking (`da067599c^..3e87c6d3b`); the test signing mock verifies everything (flip at `6071d0f13`).
Everything to resume is in `security-bisect.md` (candidate lists, driver, oracle `REAL_SIGNING=1`, probes done,
open rulings: blast-radius list, honest mock). Also parked here:
- `app.module.action.signing.sign` — the SignOptions slot on http.request / download / upload; an action handler
  doubling as a value type (the last of #19's three `clr`-named items).
- Secrets as a value type — a secret that writes itself `****` on Out and in clear on Store, so `[Masked]` could
  retire. Until then the `[Masked]` machinery (`item/this.cs:583`, `kind/reflection:259`, `filter/Tagged:127`) has
  no production user; it stays covered by the test-only `MaskedItem` (`MaskedAttributeTests`). Belongs with the
  births/value-type work.

## Done

- **24 `test.Create` static** — `app.Test.Create(goal, context)` makes the whole test (tags, coverage, skip,
  exclusion); discover just asks; test ctor has no context: `da601665c`.
- **15 Action return type** — `action.Return` is the type object; the name door reads `list<path>` as
  {list, path}: `b342137e2`.
- **14 Static type tables** — primitive tables are `app.Type`'s instance data, the type ctor no longer
  canonicalises, `GetPrimitiveOrMime` / `ClrFromMime` / `type.FromMime` gone (`96eba3f70`, `e9fb08c27`); format →
  type (`app.Type.Mime` / `.Extension`, format keeps format facts): `f2d582b72`.
- **27 LLM closed-set options** — closed (see architect).
- **20 Actor as a choice** — `choice<actor>` over `{system, user}` on the 5 slots, `app.Actor[name]` collection,
  GetActor/Convert/Resolve/Choices gone; environment.run's dead `Actor` slot now wired (see commit).
- **19 `clr`-named wrappers** — list actions return the native list (`6031fdfd7`); setting.set/remove return no
  value, `{key, value}` wrapper + static `type` class gone, `[Masked]` covered by a test-only item (see commit
  after `6031fdfd7`). signing.sign moved to #23.
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
