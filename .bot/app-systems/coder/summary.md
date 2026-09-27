# coder — app-systems

**Version:** v7 (in progress — stage 7)

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

## Next / waiting
- **7e-3** identity + permission into settings (storage move + no-fallback marker only).
- **7e-2b-ii** (setting.save/remove in, get/set out) and **7f** (concept types in the type list, the
  prompt line, teaching setting classes): builder-visible, one eval for stage 7.
- Before stage 7 closes: fold `all`/`Every` into one lazy `all()`.
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
