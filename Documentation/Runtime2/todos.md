# Runtime2 TODOs

> **OBP shape violations** go in their own running collection: `Documentation/Runtime2/obp-cleanup.md`.

Open entries only, grouped by theme, each dated with its original date; file:line is as of the last triage. An entry marked *unverified* could not be judged from code. Finished and stale entries are in git history.

## stores per actor and an app per request

### 2026-05-07 — fork-site Variables isolation beyond parameters

Concurrent writers to a goal channel run the goal body on the one Actor.Context (channel/type/goal/this.cs:117-149); decide/verify whether body `set`s must be isolated per write (unverified; parallel tasks are already isolated via context.Child).

### 2026-09-27 — variable storage through `app.store`

plang steps cannot save/load values through `app.store` (app/store/this.cs): `set %user.name% = "ingi", store by identity, encrypt idp` and `load %user% by identity idp` need an action; each identity's data in its own optionally encrypted database. Parked until app-systems closed.

## snapshot and resume

### 2026-05-02 — Callback (context for callstack design)

Decide whether a resumed run's call chain links back (Cause) to the run that issued the callback or is a clean break; nothing in app/snapshot or app/call records it. Rest of the entry is design record and can be archived.

### 2026-05-11 — E2E PLang tests for full-app Snapshot round-trip

PLang tests for snapshot save, write to disk, read in a fresh app and resume are planned in test/plan/snapshot/start.goal (Stage1-3) but not written; only the three callback tests exist.

### 2026-05-20 — Replace `!ask.answer` sentinel with Answer param

`output.ask` still gets its resumed answer through the `!ask.answer` variable (`app/module/output/ask.cs:56-83`) instead of an explicit nullable `Answer` input bound by the resume entry.

### 2026-05-20 — Per-channel serializer for stateless suspend

Stateless suspend still needs a decision on section filtering (ask: CallStack+Variables; error: plus Errors trail): per-channel allowlist vs per-kind serializer. unverified against `app/snapshot/serializer/`.

### 2026-05-20 — Relocate App.Snapshot() to Snapshot.Capture(ctx)

`App.Snapshot()` and its `Capture(owners)` loop (`app/this.Snapshot.cs:16-33`) still build the snapshot; move the loop onto `snapshot.@this` as the factory and drop `App.Snapshot()`. Pure refactor.

### 2026-05-20 — Revisit Snapshot.ResumeChain shape

`Snapshot.Resume` still walks the restored call chain through the recursive `ResumeChain` (`app/snapshot/this.Resume.cs:28`), pushing parent frames without executing them; find a shape where Restore pushes in order and one RunFrom walks naturally.

## llm provider and writer

### 2026-09-23 — property rows as plang reflection (with actions as LLM tools)

Actions-as-LLM-tools: a type should write its own JSON-schema face instead of the `MapPlangTypeToJsonSchema` switch (llm/code/OpenAi.cs:777) and the tool description is still "" (OpenAi.cs:175). Open question: a kind is a name, the menu/schema need the declared type entity.

## events

### 2026-05-31 — Event-driven parallel branch (fan-out)

An event handler cannot return a fork instruction that runs the remaining steps in N parallel scoped branches; design still a seed (return type, fork point, scope isolation, fan-in, relation to `loop.foreach`). unverified beyond absence in `app/event`.

### 2026-07-05 — Debugger runtime toggle: suspend vs teardown

Debug has no teardown or suspend: `Activate()` (module/debug/this.cs:57) binds and nothing unbinds, so `Debug = null` does not stop it. Binding `Remove()` exists (event/binding/this.cs:62); design suspend (gate the debug channel sink) vs teardown, and whether debug becomes a producer under a `diagnostic` node.

## typed values and wire

### 2026-05-15 — Typed-value pass: Dictionary -> List<Data>

`Error.Details` (`app/error/this.cs:83`) is still `Dictionary<string, object?>` and built in 5 places; make it Data-shaped so `%!error.Details.KEY%` navigates, and pin it with one .goal test. Clusters A, B and D are gone or changed shape.

### 2026-06-03 — Render a `table` to a UI (table,html)

`table` has a csv serializer/reader (`app/type/table/serializer/`) but no `(table, html)` writer to draw a grid in a UI (nor xlsx write-back).

### 2026-06-05 — Collection mutation: copy-on-write

Mutators (`add`, set-item, set-path, `remove`, `sort`...) mutate shared lists/dicts in place, so `set %x% = %y%` then a write through `%x%` can change `%y%`. Wanted: uniform copy-on-write behind a cheap "shared?" flag. unverified how aliasing behaves now.

### 2026-06-06 — Per-path lazy narrowing

First touch of a json-backed value still materializes the whole tree; per-path narrowing (only the navigated spine, siblings left raw `item(kind=json)`) is not built. Low priority. unverified.

### 2026-06-06 — Centralize value serialization to IWriter; purge STJ attrs

Replace `[JsonIgnore]`/`[JsonPropertyName]`/`[JsonConstructor]` on value/domain classes (about 66 + 7 under PLang/app) with the PLang-native tags (`[Out]`/`[Store]`, plus a rename and a construct-from-wire marker) so a second IWriter needn't know STJ; only the json writer translates. Per-type converters already collapsed to `type/item/kind/json/converter.cs`.

### 2026-06-12 — remove type converter (Judge) from Data; ctor (name, item)

Data's ctor still accepts a raw object and a declared type (`data/this.cs:248`) and `Declare` reconciles it (`:333`); finish making Data only hold an item (`:301` form) and move declared kind/strict into each type's reader.

### 2026-07-12 — materialize-on-foreign-output for sources

A content source writes raw into every format (type/item/source.cs:362). Make a foreign-format write materialize and render the value (so `write out %date%` on an untouched `{datetime}` renders culture-formatted); first audit that every declarable source type has a (type,kind) reader.

### 2026-09-24 — runtime registrations (a special session)

There is no plang door to register a file extension as a format at runtime (`.abc` read as json); the type list's `Extension(...)` lookup (type/list/this.cs:151) and Registry (type/list/Registry.cs) are the place. Needs its own design session.

### 2026-09-26 — argument rows as their own type

goal.call's `Parameter` (and ui.render's) is a plain `list` (module/goal/call.cs:23), so plain list slots and argument-row slots share one type. Give argument rows their own declared type so the refusal keys on it.

### 2026-09-27 — a `nothing` type?

Decide whether plang needs a `nothing` type beside the `null` type (type/this.cs:210) and `item.@null`: whether a value may be missing is the slot's fact.

### 2026-10-14 — Data copied into a type in one call: `Copy(type, context)`

Add `data.Copy(type, context)` returning a copy born as that type, move `data<T>.Settle` (data/this.cs:858) onto it and delete `Declare` (data/this.cs:333).

## crypto and settings

### 2026-05-05 — crypto.encrypt/decrypt real impl

Replace the identity pass-through in app/module/crypto/encrypt.cs (and decrypt.cs) with AES-256-GCM keyed from the identity key provider; both take/return binary.

### 2026-05-11 — Settings encryption-at-rest decision

Decide whether settings encrypt transparently in the store (Sqlite keyed by identity key) or the caller encrypts via crypto.encrypt, plus key rotation and plain-to-encrypted migration, before secrets-bearing modules (LLM keys, db strings) land. Blocked in practice by crypto.encrypt still being a pass-through. Store is now PLang/app/store/code/Sqlite.cs.

### 2026-06-15 — DateTime->DateTimeOffset offset policy runtime config

`datetime` lifts a CLR `DateTime` with the machine-local offset (`type/item/datetime/this.cs:38,57`), so signed data canonicalizes differently per machine. Make Unspecified handling a setting, default UTC.

### 2026-07-05 — Environment as a rich object (Culture folds under it)

Environment is still a flat text setting and Culture sits beside it on `app.setting` (setting/this.cs:23,27), not under it. Decide whether Environment becomes an object holding culture and other environment-scoped config.

### 2026-09-26 — navigation reaches any public property (security)

Variable navigation (kind/reflection/this.cs:25-27 `Descend`/`Reflect`) reaches any public property, not only the `[Out]`/`[LlmBuilder]` face, and does not mask `[Sensitive]` leaves, so `%!app.….PrivateKey%` prints a key. Make navigation honour the declared face. Belongs with parked security work.

## builder

### 2026-07-21 — MILESTONE: self-hosted builder green (open sub-items)

Build validation only checks a required property is emitted (goal/step/action/this.Validate.cs:40), not that its value is non-null, so a null-valued required param passes build and fails at run (MissingRequiredParameter). Drop the `parameters` alias in goal/step/action/serializer/Reader.cs:83 once all `.pr` regenerate canonical (unverified that they have).

### 2026-09-26 — nano double-escapes `\n`

The model still doubles `\n` in literals; Cover only refuses with a named message (goal/step/this.Validate.cs:81). Consider substituting the step's own literal when the answer differs only by escaping.

### 2026-09-26 — builder checker gaps and a question (4d)

A `%variable%` inside a quoted text still counts as covering the step's variable. Needs a rule that a step passing `%x%` alone gets it as a value (without refusing `"hello %name%"`). goal.call question is settled: arguments stay as written.

### 2026-09-26 — a template render opens every variable in scope

`ui.render` opens every variable in scope before rendering (module/ui/code/Fluid.cs:163-166), so %MyIdentity% can create an identity as a side effect. Bind only the names the parsed template (and its includes) read; reading %MyIdentity% in a render must not create one.

### 2026-09-26 — builder check: an unquoted word the step relies on

`write %message% to echo` can still build with no channel and pass every check: Cover ignores unquoted words (build-checks.md:33-44). Options: the action checks its own phrasing (output.write knows `to X`), or the author writes the step in formal.

## small

### 2026-05-29 — Audio transcription module: voice-agent reference

When an audio transcription module is built, read https://nemorize.com/roadmaps/building-real-time-voice-agents-from-scratch as design reference. No module exists yet.

### 2026-06-06 — Error messages show PLang type name not CLR GetType().Name

Sweep the remaining `GetType().Name` in error text where the value is a plang item (`goal/list/this.cs:120`, `type/item/this.cs:833`, `goal/step/action/this.Item.cs:18`); raw-CLR fallbacks may stay.

### 2026-06-11 — text .ToString() -> Clr<string>() sweep

Replace `.ToString()` on `text.@this` receivers that feed a .NET call with `Clr<string>()` (e.g. `module/variable/set.cs:112,175`); keep ToString for display/logs.

### 2026-06-12 — Data.Created/Updated DateTime -> DateTimeOffset

`data.Created`/`Updated` are CLR `DateTime` (`PLang/app/data/this.cs:195,290,311`); switch to `DateTimeOffset` (the plang `datetime`) and audit readers.

### 2026-07-21 — ActionError should take an action, not a step

`ActionError(message, Step, ...)` (error/ActionError.cs:15) still takes a step; `Error.Action` exists (error/this.cs:170). Give ActionError an action-carrying ctor and move step-only callers to it.

### 2026-07-21 — clr-leak sweep follow-ups (goal-graph-singular)

Rename the stale `ActionEntity` alias to `Action` in PLang/app/call/Position.cs:1,7,16 (the only remaining use).

### 2026-09-26 — go over the sync-over-async calls

8 sync-over-async sites remain (data/schema/signature.cs:122, terminal/code/Sandbox.cs:140, module/code/this.Snapshot.cs:116, ui/code/Fluid.cs:338,375, actor/this.cs:156, keepalive/this.cs:24, PlangConsole/Program.cs:18): await each, or keep with a comment naming the third-party sync boundary. No new ones.

### 2026-09-26 — a goal's .pr is an object: `pr: {path}`

`goal.PrPath` (goal/this.cs:112) should become `pr` with a `path` member (`%!app.goal["/show"].pr.path%`) so `pr.encryption` etc. can hang on it. Later, after app-systems.

### 2026-10-17 — scan for carriers opened only to be handed on (decision 851)

Scan handlers for a `.Value()`/`.Use(...)`/`Given()` whose only purpose is to pass the value to the next owner, and owner parameters typed as values where the carrier could flow; pass the carrier. Also CLAUDE.md:21 still states the replaced rule.

## design idea (deferred)

### 2026-05-06 — mobile signed code (actions.run)

Signed mobile code: a server ships an actions list as Data over a channel; client verifies the signature and runs it in a level-0 sandbox via a new `actions.run`. Needs a privilege level on the actor and a mime for actions-as-data. Defer until a concrete consumer.
