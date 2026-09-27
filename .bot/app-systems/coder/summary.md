# coder — app-systems

**Version:** v8 (in progress — stage 8; v7/stage 7 closed)

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages. v1–v6 covered stages 0, 1, 3–6; v7 is
stage 7: every concept is its type (goal, module, actor, test, variable) and settings become typed C#
classes under their owners. Stage 7 is carved into slices, each green and pushed on its own; the trace,
rulings and as-built notes for every slice are in `v7/plan.md`.

## What was done (v7 so far)
- **7a goal** — `app.goal` a `type<goal, goal.list>`; `all()` lists every `.pr` lazily; sub-goals
  `parent#name`.
- **7b actor, module** — both their types; `Formal` born with the modules its caller awaited once; the
  `.pr` reader uses module.list's internal lookup (decision 38, a reversal for Ingi).
- **7c test** — `app.test` always exists; testing = an open test session, a channel kind
  (`app/channel/type/test`); a test's writes land only in its Stdout (no longer live on the console).
- **7d variable** — `IList.Of(context)`: the asker's list; `app.variable` over a view of
  `context.Variable`; C#'s `app.variable.list` throws.
- **7e-1** — `app.store` (abstract, sqlite kind); settings machinery per actor. 664dfc3a4 holds only
  deletions (a failed `git add` in an `&&` chain); d4de3fb80 the rest. Store dispose fix 74f9dc8f9.
- **7e-2a** — setting classes (kinds of `setting`, path = namespace, a module's own by its name); `%!path%`
  reads; owners answer `.setting`. Fixed `%!build.cache%` (NotFound since stage 6).
- **7e-2b-i** — one row per actor per setting; layers defaults ← row (user, else system) ← this run ←
  the step; the action-param seam reads rows; `Storage` gone.
- **7e-2c** — CLI flags are this run's values for their owners' classes (test, build, debug, callstack,
  app); rows load at app start, then settings build in memory; Debug/CallStack hold theirs and
  `app.Refresh` rebuilds them on a write. Fixed a re-entrant row load and a stale debug `.gitignore` rule.
- **Results each slice:** six suites no new failures vs baseline; `plang --test` 7/0/317; builder goals
  rebuilt byte-identical where the runtime seam changed.

## OBP cleanup before 7e-3 (decisions 53–66; each slice OBP-reviewed by plang-cd before the next)
- c2d150d51, 49a2e97e3 — settings nodes answer their own hop; errors are results; one convert walk; the
  seam reads `context.Setting.Get(action, option)`.
- 4fff826d0 — `parser.Whole` the one variable maker (no statics); `Every(setting, context)` reads the
  asker's settings (`Every()` = the system asking); the convert walk is `setting.Apply(values, context)`.
- e1e6261c3 — the variable routes its own `%!…%` write (root hop → settings; action/module nodes write
  the run layer); variable.set's branch gone.
- 56cd06fb4, 4ab7f7d19, 9e4b0c241 — a test run is `app.test.list.Start`; a test runs itself
  (`test.Start(app, context)`); coverage watches itself; `new app(parent)`; the report writes itself
  (`report.Write(context)`); test.start / test.report are one line. **Builder-visible:** test.start's
  Parallel/Timeout and test.report's Format params are gone — `%!app.test.setting.*%` owns them.
- e6910e699, e1b262dfe — the type door closes `choice<T>` (`kind.Of(type)`); the setting walk makes a
  slot through `type.list[clr].Create`.
- 477caa145 — Debug reads its `Setting` (8 forwarders gone); debug's options are plang types.
- **Open:** the walk's `.Clr` lowering still re-tags typed-list options (`list<text>`) — the door can't
  make `list<T>` (asked plang-cd). The python-decider golden `PickListTests.TheStageOneRequest…` is red
  on purpose until 7f regenerates it (catalog examples changed). 8 stale `.test.goal` files edited to the
  setting form need their first build with 7f.

## After the cleanup (decisions 68–76)
- 87b66b528 **7e-3** — identity (the system's row) and permission (per actor) are `[Own]` setting classes;
  `Load()` answers an unreadable row and Save/Remove refuse to write over it; typed-list rows read back (the
  kinded list reader reads the Data rows a list writes — decision 69); Build.goal's dead `%!build.summary%`
  step removed (slice 4 had made every `plang build` fail there).
- bbbb45925 **7e-2b-ii** — `setting.save` / `setting.remove` take a setting; `get`/`set` gone.
- **7f — closed** (`v7/7f-result.md`): concept types in the type list; settings taught in prompt C only where a step
  names one; the `%!app.X["key"]%` line likewise (in the system prompt it broke `start`); `save %!x%` taught apart from
  file.save; test's settings are `%!app.test.setting%` (Ingi). Eval round 16 run 3: golden 58/58, bootstrap 8/2/2
  (per step, decision 83). 8 of 10 rewritten test goals pass; the 2 Masks tests wait on `%MyIdentity%`.
- **Stage 8** (plan `v8/plan.md`, decision 75): c66b52541 **8a** — events and `on` on every object (classes,
  bindings, the shared empty `on`; nothing fires yet). 8b onward waits for 7f's eval.

## Stage 8 so far (plan `v8/plan.md`)
- **8b** — goal/step/action `on.start` fire (`Before`/`After` on the event, one call each); `goal.Load` via
  `item.ILoad<T>`; app `on.start` around the entry goal (e75f05552).
- **8c** (046ba8af6, 5f6b49433) — a channel is an item and fires only its own write/read/ask events
  (decision 105); per-flow re-entrancy guard; `channel.WriteText` one non-virtual door (841fad1d7).
- **8d** — variable set/remove events on the list; callstack diff capture and the debug watch bind to them.
- **Naming (decision 100 B, 39345235d)** — `type.Namespace` is identity, `[PlangType("word")]` the name.
- **No swallowed errors (decision 107)** — d8342f33e, 6d34adb92, 2a0d34828 (+ b6e7553b7 file.read probe
  warning): exceptions bubble or become the error result; transport doors keep catching I/O into an error Data.
  Build's create-app prompt goes through the ask door.
- **Decision 110 (throwaway decode channels) — HELD** pending Ingi's one-format-list shape. Facts trace:
  `v8/mime-registries-trace.md`. Already decided for the build: url refuses unsigned `application/plang` (same
  as http); decoded Data must be born with the caller's context; deleting `channel/type/file|http` is Ingi's call.

## Next / waiting
- Ingi's format-list shape → then the decode door (decision 110).
- 8e–8h per `v8/plan.md`.
- **Waiting on Ingi:** kind faces; `SetTests.Validate_TypeMismatch_ReturnsError`; decision 38; the
  live-console change for tests; module settings under `%!app.module.<m>.setting…%`?

## Code example
An owner reads its setting in memory; a CLI flag and a plang `set` are this run's values; a save is the
actor's row:
```csharp
var test = context.Setting.Of<app.test.setting.@this>();          // defaults ← row ← this run
app.System.Setting.Set("app.test.setting", cliDict);               // --test={"timeoutSeconds":5}
// - set %!app.goal.list.setting.os% = false                       (this run, through the owner)
await actor.Setting.Save("app.goal.list.setting", data);          // user!app.goal.list.setting
```
