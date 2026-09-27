# coder v7 — stage 7 plan: every concept is its type

Branch `app-systems`. Plan: architect `plan.md` stage 7 row, "One list, one type" (`all`), and
"Settings (stage 7)". Slices from plang-40, each green against the baseline and pushed on its own:
7a goal → 7b module + actor → 7c test → 7d variable → 7e settings → 7f concept types in the type list + the
prompt line (twins + one eval). Trace first per slice; this file opens with 7a.

## 7a trace — goal

**Today** `app.Goal` is `goal.list.@this` (`app/this.cs:145`), a plain class over three dictionaries
(`_goals` by PrPath, `_byPath` by Path, `_byName` fuzzy by name) with `Get(string)` scanning forms (name,
`.goal`, `/leaf`, PrPath, slash-qualified). Production callers of the collection are few:

| caller | uses |
|---|---|
| `module/action/goal/call.cs:80,87` | `GetAsync(name, caller)` — call's bare-name lookup (caller chain → cache → load `.pr` near the caller, then root/system) |
| `ui/code/Fluid.cs:390` | `GetAsync(goalName)` (no caller) |
| `module/action/build/this.cs:112`, `test/start.cs:206` | `Load(pr)` |
| `callstack/this.Snapshot.cs:212-213` | `Get(goalName) ?? Get(goalPrPath)` (snapshot restore; its structure is parked, #18) |
| `goal/setup/this.cs:25,61` | `AllIncludingSetup`, `Add` |

Tests: `Add` ×104, `Setup` ×28, `Load` ×11, `GetAsync` ×9, `this[…]` ×7, `Get` ×6, `list` ×2, `current` ×1.

goal is already an item with `ICreate` (`goal/this.Item.cs`); it has `Address` (`goal/this.cs:216`, the
.goal path without extension) but no `Match`/`Current`/`List`. A sub-goal's `Parent` is set by the `.pr`
reader (`goal/serializer/Reader.cs:44`) but not by `goal.Parse` (`goal/this.cs:632`, `Child.Add` on a
naked `List<goal>`); a sub-goal's `Address` is its file's, same as its parent's.

**Shape (as the plan says):**
```
app.goal                  type<goal, goal.list>        (app/this.cs: `goal`, lowercase)
app.goal.list             goal.list : list<goal>       rows are the loaded goals; Add replaces the same PrPath
app.goal.list.all(setting) goal's override: the .pr files under .build/ of the app and of /system/,
                          app's copy winning; setting { os = true, visibility = [public] }
app.goal.Get("/show")     walks all(): goal.Match(key) — its Address, or a child's `/start#show`
app.goal.current          goal.Current(context) — the running goal
list navigation `.all`    base list word, before the empty check (type/item/list/this.cs Get)
```
Goes: `_goals`/`_byPath`/`_byName`, `Get(string)`'s form scans, `this[string]`, `this[path]`, `list`
(the property), `Names`, `All`, `int Count`, `Public`, `Events`, `Contains(string)`, `Remove(string)`,
`Clear`, the stale "no app-level current" comment. Stays on the list as its work: `Load(pr)`, `Setup`,
call's lookup.

## Before stage 7 closes (plang-40)

- **One name for `all`.** `all()` answers its list at once and walking it is async: `list<T> :
  IAsyncEnumerable<T>` (the base list's enumeration is its held items; goal's `all` is a lazy list that
  loads each `.pr` as the walk reaches it). `Every` goes; `type<T, L>.Get` is `await foreach (var p in
  list.all())`. `all()` with no argument is every default (the shared empty setting), not
  `setting = null`. If a sync member (`Count`, `Items()`) would have to load synchronously, report the
  snag instead of forcing it.

## 7b trace — module and actor

**module** — `app.Module` is `module.list.@this`, a registry over a name → module dictionary: `Discover`,
`RegisterType`, `Register` (the registry's own work), `this[name]` (throws), `Contains`, `Names`, `list`
(a fresh plang list per ask), `GetActions`, `GetActionType`, `int Count` (counts actions), `All`
(instances, for disposal), `Remove`, `Clear`, `Teaching`. `module.@this` is a plain host class (not an
item). Production uses of the collection: `this[name]` ×12, `Contains` ×5, `Remove`, `Discover` — in
the step list's validation (`goal/step/list/this.cs:131`), the `.pr` reader
(`action/serializer/Reader.cs:53`), `goal.Parse` (`goal/this.cs:366`), pick (×8), Formal (×2), module.add /
module.remove, test.report. Tests: `this[…]` ×172, `Register`/`RegisterType` ×21, `Contains` ×10.

**actor** — `app.Actor` is `actor.list.@this`: `System`, `User`, `this[Name]` (a `choice<actor.Name>`),
`DisposeAsync`. actor is already an ICreate item. Production `this[Name]` ×6 (goal.call, event.on,
environment.start, channel.set/remove), all in async handlers.

**Shape:**
```
app.module   type<module, module.list>   module.list : list<module>; Discover/Register/RegisterType/Teaching stay
module       an ICreate item (declines creation), Match by Name; Action / Modifier / this[action] stay its own
app.actor    type<actor, actor.list>     actor.list : list<actor> holding System and User (still named members)
actor        Match by name (system / user); Current = the asker's actor
```
Goes: module.list's `this[name]`, `Contains`, `Names`, `list` (the class is the list), `GetActions`,
`GetActionType`, `int Count`, `All` (→ the disposal walks the modules' instances itself), `Remove`/`Clear`
as the registry's own names (a module's removal empties it, as today); actor.list's `this[Name]`.

## 7b — what doesn't hold as written

1. **Synchronous callers of a module by name.** The `.pr` reader is a synchronous ref-struct pass, and
   Formal, `goal.Parse`, pick and the step list's validation run synchronously; `app.module.Get(key)` is
   async (it walks `Every`). Six production paths can't await it without sync-over-async. Proposal: the
   module list keeps one synchronous door by name — `module.list.Named(name)`, null on a miss (no throw),
   the walk over its items that `Match` would do — used by those callers; `app.module.Get(key)` is the
   door for everything else (handlers, plang navigation). Alternative: make those paths async (the `.pr`
   reader can't be: it is a `ref` reader).
2. **Tests' `app.Module["x"]["y"]` ×172** become `app.module.list.Named("x")!["y"]` (a script), or, if you
   rule the sync door out, a test extension in `PLang.Tests/Shared/`.

## 7a — as built (commit da26b54dd; its message wrongly repeats "stage 7 plan, 7a trace" — the scratch
message file didn't update; not rewritten, it's pushed)

All eight answers taken as ruled; call's lookup is `goal.list.Find(name, caller)`. The walk under
`all(setting)` is `list<T>.Every(setting)` (internal, lazy); `type<T, L>.Get(key)` walks it, so the goal type
stops reading at its match. Every yields the goals already held first (no reading), then each listed
`.pr` not held. `goal.list.setting.Of(goal)` answers which of a file's goals a setting lists.
Tests: GoalsTests and GoalAccessorTests rewritten for the new doors; sync `Get(name)` sites use
`await …Find(name)`; tests asking "is it held yet" check `goal.list.Items()`. A test binary has no `os/`
beside it, so the system listing is empty in tests.

## 7a — what doesn't hold as written, with proposals

1. **"Lazy" `all()`.** The default is "the public goals, one per `.pr`, from the listing alone", but a
   `list<goal>` holds goals, and a goal's facts come from its `.pr`. Proposal: `all()` lists the `.pr`
   files once and reads each (through the list's own `Load`, so a goal read once is the same instance);
   the answer is cached on the list until a goal is added. A `.pr` that doesn't read (an old format) is
   left out and named in a warning, not fatal. The alternative — a lazy goal shell that reads its `.pr`
   on first touch — means every goal member guards a load; I'd avoid it.
2. **Visibility "derived from the parent".** Visibility is stored (`[Store]`, written and read in the
   `.pr`). Proposal: `Visibility => Parent == null ? Public : Private`; the `.pr` still writes it (same
   bytes), the reader stops reading it. The build's `Parse` already assigns exactly this.
3. **Parent set where the child is added.** `Child` is a naked `List<goal>`. Proposal: `Child` becomes a
   small `list<goal>` of the goal's own (`goal/child/this.cs`) whose `Admit` sets the child's `Parent` —
   then `Parse`, the reader and anyone else adding a sub-goal get it for free. (Admit is today a guard;
   this makes it the one place a slot is taken, which is what it is.)
4. **Sub-goal address.** `Address => Parent is { } p ? $"{p.Address}#{Name}" : path-derived`. Callers
   that read `Address` today for a sub-goal (pick's `call` check, `Get`'s slash-qualified scan) —
   traced, both only ever see top goals or compare names.
5. **Snapshot restore** (`Get(goalName) ?? Get(goalPrPath)`, parked structure): keep its behaviour by
   walking `app.goal.list` for the goal with that PrPath (then name) — no new name lookup on the list.
6. **Call's lookup** (`GetAsync`): it is the list's loading work (caller chain, near folders, root,
   `/system`), used by `goal.call` and Fluid. Proposal: it stays on `goal.list`, named for what it
   answers — `Callee(name, caller)` ("the goal a call names"), beside `Load`. (`goal.Callee(context)`
   already exists on goal for "every goal this goal reaches"; a different question on a different
   owner, same word — or `Called`, if you'd rather they differ.)
7. **Setting class before 7e.** `all(setting)` needs `goal/list/setting/this.cs` now; 7a adds the plain
   class (`Os = true`, `Visibility = [Public]`) and the dict → class step; `ISetting<T>` and the builder
   reading it land in 7e.
8. **Adding keeps one goal per PrPath** (today's dictionary semantics, which tests rely on — `Add` ×104):
   goal.list overrides nothing on `Add`; it removes the goal already at that PrPath first (its own
   `Add(goal)`, the typed door beside the base's untyped ones).

## 7b module — as built

- `module.@this` is an item (`ICreate` declines, `IMatch` by name ignoring case, `ICurrent` none);
  `module.list` is a `list<module>` (Discover, Register, RegisterType; `Element` get-or-creates under its
  lock). `app.module` = `type<module, module.list>`; `Run<T>` awaits `app.module.Get(name)`.
- Readers (the `.pr` action reader) use the internal `module.list[name]`, which throws on a miss; the goal
  load turns the throw into a result (ruling (b), a reversal of "higher up the stack" — for Ingi).
- `Formal(step, modules)`: its async callers (`step.list` validation, `pick.list.Take`) await
  `app.module.list.all()` once and hand it in. `pick.list` holds them by name for the answer it takes.
- `module.remove` finds via `app.module.Get`, removes from the list, and empties the module.
- Tests: a test-side `app.Module(name)` (PLang.Tests/Shared/ModuleTestExtensions) — the app's module, or
  an empty one outside the app for an unknown name (tests that name a bogus module). `%!app.module.list%`
  (Decide.goal) is guarded by `ModuleAccessorTests.AppModuleList_ReadsAsAVariable`.
- Suites: no new failures vs baseline (Runtime 22, Data 36, Generator 10 — at or under). plang --test
  7/0/317 after a clean rebuild.

## 7c trace — test

**Today** `app.Test` is `test.list.@this?` (`app/this.cs:184`): null when not testing, born by `--test`
(`Executor.cs:73`), by `test.start` for each child App (`test/start.cs:92`), and by snapshot restore
(`this.Snapshot.cs:59`). It holds four things in one class: settings (`TimeoutSeconds`, `Parallel`,
`Verbose` — read nowhere, `Format`, `Include`, `Exclude`), the tests (`_tests`, `Add`, `Tests`, `Count`,
`Create`, `Exclusion`), run state (`StartedAt`, `Coverage`, `Current`) and the report's reading
(`Summary()`, `Verdict()`). "Are we testing" is `Test != null`: `Mode` (`app/this.cs:197`) and through it
the in-memory settings store (`:562`) and Executor's routing to `/system/.build/test.pr` (`:136`).
`Current` is set only on a child App (`start.cs:95`); `report.cs:33,78` read `Current == null` as "not a
nested run". The tests' output is captured by a BeforeWrite binding on the child's user actor
(`start.cs:126-146`) into `test.Stdout`.

**Shape:**
- `test/this.cs` — the test; adds `IMatch` (delegates to its goal: every test goal is named `Start`, so
  the address is what tells them apart), `ICurrent` (below), `IList`.
- `test/list/this.cs` — a `list<test>`: typed `Add`, `Create`, `Exclusion`. `Count`/`Tests` go (the
  base list's count; `Items()` inside C#). Holds `Setting` and `Report` (below) — the concept's own work.
- `test/setting/this.cs` — the plain setting class (as goal's in 7a; `ISetting<T>` in 7e):
  `TimeoutSeconds`, `Parallel`, `Format`, `Include`, `Exclude`, `Actor: choice<actor.Name> = User`.
  `Verbose` goes (read nowhere). `--test={…}` applies onto it (`app.Setting.Set(app.test.list.Setting, …)`).
- `test/report/this.cs` — `StartedAt`, `Coverage`, `Summary()`, `Verdict()`; `test.report` writes it.
- `app.test` = `type<test, test.list>`, always there.

**The session — proposal (needs a ruling):**
- `test/session/this.cs : channel.type.session.@this` — kept-open, holds what is written to it as rows
  (memory), `Text` its rows as text. Knows its `Test` (null for the run's own session).
- **The run's session**: `test.start` (and `--test`) opens one on the actor its setting names
  (`app.User` by default), registered under the name `test`; it closes when the run ends.
  "Are we testing" = that actor has an open `test` channel. `Mode` derives from it; the in-memory store and
  Executor's routing follow `Mode` unchanged.
- **Each test's writes**: the child App's user actor gets its own session registered as `output` (so every
  `write out` in the test lands there, no binding), holding its test. `test.Stdout` = that session's text
  when the test completes. The BeforeWrite capture goes. The child counts as testing because its user
  actor has an open session — so the child is registered under both `test` and `output`? **Question:**
  one session under two names, or is "testing" = any open `test.session` on the actor (checked by type,
  not name)? I lean to the latter — the session is the fact; its name is where writes go.
- `ICurrent`: `test.Current(context)` = the test of the session on `context.Actor` (null outside a
  test). `report.cs`'s "nested run" check becomes `test.Current(Context) != null`. No stored `Current`.
- Snapshot restore rebuilds a `Mode.Test` App by opening a session (no test in it).

Plan: 7c-1 (independent of the ruling) = test as its type: `list<test>`, `test/setting`, `test/report`,
`app.test`, the callers. Interim for one commit: the run's report is born when a run opens
(`test.list.Report` null otherwise) and `Mode` reads that; 7c-2 (the session, after the ruling) replaces
it with the open session.

## 7c — as built (ruling (a): testing = an open session on the actor, asked through one door)

- `app.test` = `type<test, test.list>`, always there. `test` adds `Match` (its goal's address), `ICurrent`,
  `IList`. `test.list` is a `list<test>` holding `Setting`, `Report`, `Session`, `Open(test?)`, `Close()`,
  `Create`, `Exclusion(test, context)`.
- `test/setting/this.cs`: `Actor` (choice<actor.Name> = user), `TimeoutSeconds`, `Parallel`, `Format`,
  `Include`, `Exclude`. `Verbose` gone. `--test={…}` applies onto it, then Executor opens the session.
- `test/report/this.cs`: `StartedAt`, `Coverage`, `Summary()`, `Verdict()` over the list's tests.
- **The session lives at `app/channel/type/test/this.cs`**, not `test/session/`: the channel-kind layout
  rule (`ChannelKindLayoutTests`: every channel kind under `app.channel.type.*`). One object, one name:
  the run's is registered as `test`, a test's as `output` on its App (its `Text` becomes `test.Stdout`;
  the BeforeWrite capture is gone). The one door: `channel.list[System.Type]` (open channel of that
  class, any name) — `test.list.Session` (Mode, hence the in-memory store and Executor's routing) and
  `test.Current(ctx)` (report's nested-run check) both ask it.
- Behaviour change: a test's writes no longer also reach the console live; they are the test's Stdout
  (the report prints it on failure).
- Snapshot restore opens / closes the session from the captured Mode.

## For stage 11 (plang-40)

- `PLang.Tests/Shared/ModuleTestExtensions.cs` `app.Module(name)` is one of the test helpers that goes
  when tests use the app's own doors.

## For 7e (plang-40)

- `test.list.Setting` is interim: in 7e test's setting class is loaded the way every setting is (the
  actor's saved row or its defaults, this run's `set %!x%`, the step's values; `--test={…}` landing on it
  as the CLI flags do), not held on the list.

## 7d trace — variable

**Today** there is no `app.variable`; `%!app.variable…%` doesn't navigate. The memory is
`type/item/variable/list/this.cs` (moved in stage 6), one per context (`actor/context/this.cs:42`,
`context.Variable`): a `ConcurrentDictionary<string, data>` (name → Data, ignore case) plus per-call
overlay frames (`Calls`, 25 uses: goal-call parameters, forked flows), `OnSet`/`OnCreate`/`OnRemove`
(diff capture, `--debug` watch), snapshot (`ISnapshot`, `SnapshotAt`), `Clone`/`Save`/`Restore`. Its
production surface: `Set` ×26, `Get` ×10, `Snapshot`/`Remove`/`Ensure`/`Calls` ×2, `Replace`/`Count`/`Clear`
×1. `variable.@this` (the element class) is the *reference* — `Text` + `Code` — with `Name`, `Start`,
`Set`, `Ensure`, `Replace`; it has `ICreate` but no `IMatch`/`ICurrent`/`IList`.

**What the plan asks:** `app.variable` a `type<variable>`, "its list is the memory of the actor in
play, reached through navigation's context"; `Get(key)` takes no context (decision 8);
`%!app.variable%` → `list` (names), `%!app.variable.user%` → `name`, `type`; `on.event(item:
%!app.variable.user%, …)` binds to that one variable.

**Where it doesn't fit `type<T, L>` as built (stages 4 and 7a–c):**
1. `type<T, L>` builds its list once per app (`T.List(app)`, `type/this.Generic.cs:13`); `Get(key)` walks
   that list. Variable's list is per asker — there is no app-wide one to build.
2. `L : list<T>` — the memory isn't a list of `variable`s: its rows are Data named by the variable, with
   frames layered over them. Making it a `list<variable>` means reworking the store (and storage by
   identity is Ingi's parked item).

**Options:**
- **(a) The generic type learns the asker's list.** `IList<T, L>` gains `static virtual L Of(context)`
  (default: the app's list); navigation (`Get(parent, key)`, the `list` member) asks `T.Of(parent.Context)`;
  C#'s `app.X.list` stays for the app-wide concepts. The memory gets a *view* that is a `list<variable>`:
  `context.Variable.list` — one `variable` per name held (overlay first), born on read, not stored (the
  dictionary stays the one store). `variable` gets `Match(name)`, `Current` none. C#'s `app.variable.list`
  has no asker: it throws, or isn't offered for this concept (a C# caller uses `context.Variable`).
- **(b) Variable's type is its own small class** with the generic's face (`list`, `Get`, no `current`),
  navigating through `parent.Context.Variable`; the generic stays as is. Simpler; a second type class.
- **(c) Make the memory a `list<variable>`** — out of bounds (storage by identity is parked).

I lean to (a): one type class, and the only new thing it learns (a list that belongs to the asker) is
exactly what the plan says variable is. The `type` fact of `%!app.variable.user%` is its value's type
(the variable reads its row's Data).

## 7d — as built (ruling (a))

- `IList<T, L>.Of(context)`: the list the asker sees; null (every concept but variable) is the app's list.
  `type<T, L>` makes its app list lazily (`list`, on first read) and navigation always asks
  `Of(parent.Context) ?? list` — the `list` member and a key naming one X both; C#'s `Get(key)` uses `list`.
- `app.variable` = `type<variable, list<variable>>`. `variable` adds `Match(name)` (ignore case), no
  `Current`, `List(app)` throws "a variable list belongs to an actor: use context.Variable", `Of(context)`
  = `context.Variable.list`.
- The memory's view `context.Variable.list`: one variable per name, the current call's overlay names first
  (`call.Names`, inner scope first), `!` settings left out, names the parser can't read back whole
  skipped. Born on each read; the dictionary stays the one store (its shape untouched).
- One variable navigated as itself: its members by reflection; its `type` is the type of what it holds.
- Tests: `VariableAccessorTests` (list, key, name, type, NotFound, overlay order, C# list throws).

## 7e trace — settings

**Today.**
- `app/setting/this.cs` is one class for both lifetimes (`Storage.InMemory` / `Storage.Persistent`). The
  in-memory side is a per-context chain of string-keyed Data (`context.Setting`, parent
  `Parent?.Setting ?? App.Setting`, `actor/context/this.cs:134`); the persistent side is the `settings`
  table of `App.SettingsStore` keyed by the path's first segment (`:43-60`, an unset one is an `AskError`).
  It also holds the CLI convert-walk `Set(object node, dict)` (`:82-136`).
- Writers: `set %!x%` → `context.Setting.Set(InMemory, "x", value)` (`variable/set.cs:127-133`);
  Executor's `llm.cache` off (`Executor.cs:130`). Readers: the generated action-param seam only —
  `context.Setting.Get(InMemory, "module.action.param", "module.param")` (`Generators/Emission/Property/
  Data/this.cs:150`). **A plang read of `%!x%` does not reach the setting chain today**: the root hop
  asks the variable store for `!x` (`code/Variable.cs:18`), which only holds the bindings (`!data`,
  `!app`, …). `%!llm.cache%` reads NotFound.
- The CLI walk lands on objects, not setting classes: `app.Debug`, `app.test.list.Setting`, `app` itself
  (`--app`), each actor's `CallStack`, `app.Build` (`Executor.cs:64-117`).
- `App.SettingsStore` (`IStore`, sqlite / in-memory in test mode) is used by: `setting` (the table above,
  and the `setting.get/set/remove` actions — the key-value door), setup (`goal/setup/this.cs:107,142`),
  the LLM cache (`OpenAi.cs:66`, `TypeSafe.cs:30`), identity (`identity/code/Default.cs:208-278`, one row
  per identity), permission (`actor/permission/this.cs:62,99,129`, grants filtered by actor by hand).
- `.goal` / `.pr` uses of the `setting` module in `os/` and `Tests/`: none. `%!x%` reads that exist are
  bindings the Executor or a goal puts in the memory directly (`%!build.cache%`: `Executor.cs:121`
  `userVars.Set("!build.cache", …)` and `Build.goal:7` `set default %!build.cache%`), `%!plang.*%`,
  `%!step.Text%`, `%!trace.id%`.

**Slices (proposal):**
- **7e-1 — homes.** `app.store` (`app/store/this.cs` over today's `IStore`; `SettingsStore` goes; setup
  and the LLM cache stay its owners). The machinery moves to `actor/setting/this.cs`: each actor has one
  (`app.System.Setting`, `app.User.Setting` falling back to the system's), a context's this-run layer
  chains to its actor's (`Parent?.Setting ?? Actor.Setting`); Executor's `llm.cache` lands on
  `System.Setting`. `app/setting/this.cs` becomes the app's own setting class (`id`, `name`, `create`,
  `environment`); `--app` lands there. No behaviour change.
- **7e-2 — typed settings.** `ISetting<T>` on the owner; one table, a row `<actor>!<class path>` →
  `Data<class>` whole; loaded in layers (row or defaults → this run → the step); the `%!…%` read reaches
  it (new: today it doesn't); `setting.save` / `setting.remove`; `setting.get/set` go; `Storage` goes;
  `test.list.Setting` becomes test's setting loaded like every other, and `--test` / `--debug` /
  `--build` / callstack land on their owners' classes.
- **7e-3 — identity and permission** into their setting classes, with the no-fallback marker. Security:
  its own slice, reviewed on its own.

**Questions (7e-2 can't start without them):**
1. **Where an action parameter's setting lives.** Today every action param is a setting
   (`%!llm.query.cache%` → `%!llm.cache%` → `[Default]`), keyed by string. With typed classes, is an
   action's setting class *the action's own class* (its properties are the options: `%!llm.query%` is
   one instance of `llm.query`'s settable properties, `%!llm%` a module class), or does each module
   declare a setting class listing the params it lets be set? The first keeps "every param is a setting"
   with no new files; the second is explicit but writes ~125 classes or drops most params from settings.
2. **The `%!x%` read path.** `%!goal.list.setting.os%` parses to root `!goal`, then `.list`, `.setting`,
   `.os`. Proposal: the root hop, for a `!` name the memory doesn't bind, asks the actor's setting for
   that name's node (the settings under `goal`), and navigation steps down to the instance and its
   property. Or is the whole class path one key (`!goal.list.setting`), read as one hop?
3. **Row key's actor.** `user!goal.list.setting` — the actor's name (`system`/`user`), matching
   `actor.Name`, lowercase?

## 7e-2 answers (plang-40)

1. (a) An action's own class is its setting class: its settable properties are the options,
   `%!llm.query%` one instance of them. A module-wide default (today's `%!llm.cache%`) is the module's
   own class `module/action/<m>/setting/this.cs`, added only where a module-level setting is set today
   (grep goals + C# for `%!<module>.<param>%` writes and Executor's; llm's `cache`, identity's planned).
   The seam: step value → this run's `set %!x%` → the actor's rows (user, then system) → the class default.
2. A `!` name the memory doesn't bind goes to the actor's settings; navigation steps down the class path
   (`!goal` → `.list` → `.setting` → `.os`): plang path = class path. Bound `!` names (`!app`, `!event`,
   `!data`, …) answer from memory first.
3. Row key's actor: the lowercase choice name, `user!goal.list.setting`.
- 7e-3 (identity, permission): its own slice; diff limited to moving storage + the no-fallback marker —
  no change to how grants are checked or keys used.

## 7e-1 — as built

- `app.store` (`app/store/this`, abstract: `Get<T>`, `GetAll<T>`, `Set`, `Remove`, `Exists`, `Tables`,
  `Dispose`) with its kind `app/store/sqlite/this` (on disk, or in memory while testing); `IStore`,
  `Sqlite` and `App.SettingsStore` gone. Callers: setup, the LLM cache (OpenAi, TypeSafe), identity,
  permission, the `setting` actions. `IStore.ResolveTableName` (no production caller) and its test gone.
- The machinery is `actor/setting/this` (`app.actor.setting`): each actor's `Setting`
  (`app.System.Setting`, `app.User.Setting` falling back to the system's through the actor list's
  `fallback`); a context's layer chains `Parent?.Setting ?? Actor.Setting`. `App.Setting` gone. The
  generator's seam emits `global::app.actor.setting.Storage.InMemory`. Executor's convert-walks and
  `llm.cache` go through `app.System.Setting` (the callstack walk per actor).
- Tests: `SettingsTests` +2 (user falls back to system; system doesn't see the user's).
- Committed in two: 664dfc3a4 holds only the three deletions (a failed `git add` in an `&&` chain let
  the commit run with just the earlier `git rm`s); the follow-up commit holds the rest. 664dfc3a4 alone
  doesn't build.
- Seen, not changed: `app/this` DisposeAsync disposes the store's `Task`, not the store
  (`_store.Value.Dispose()`), so the sqlite store is never disposed — today's behaviour, kept.
  (Fixed on plang-40's word in its own commit, 74f9dc8f9, with a test.)

## 7e-2 trace — typed settings

**Found while tracing:** `%!build.cache%` does not resolve today (probe: root `!build`, NotFound, value
null). Executor's `userVars.Set("!build.cache", …)` stores one memory name `!build.cache`, but the parser
reads root `!build` then `.cache`; `set default %!build.cache%` (Build.goal:7) writes the setting chain,
which no `%…%` read reaches. So Properties.goal's `cache=%!build.cache%` hands llm.query nothing.
The module-level settings written today: `llm.cache` (Executor, C#), `build.cache` / `build.summary`
(Build.goal). `%!ask.answer%` is the callback sentinel, a variable, not a setting.

**What a setting class is, and what one instance is** (proposal):
- Each setting class has a path, its namespace under `app.` — `goal.list.setting`, `test.setting`,
  `module.action.llm.setting` read as `llm` (a module's own), an action `module.action.llm.query` read as
  `llm.query`. The app knows them by path: the owners that name one (`ISetting<T>`), every action class,
  and each module's own where it has one (llm: `cache`; build: `cache`, `summary`, `files`).
- One actor's instance of a class, in a context = the class's defaults ← the actor's saved row (user,
  else system) ← this run's `set %!path.prop%` (the context's chain, as today) — built on read.
- **An action's instance** is the action as the catalog has it (`app.module["llm"]["query"]`, its
  `Property` rows with their defaults) copied, the layers written onto its rows: the same shape a
  `.pr` action has, so its saved row is an action (`Data<action>`) and needs no new serialization.
- **An owner's class** (goal.list.setting, test.setting, …) is an item: a small base
  (`setting.@this : item`) writes its public properties (reflection kind, like module) and reads itself
  back by property name through the same convert the CLI walk uses — so a row is `Data<the class>`.

**Read path** (ruling 2): the root hop, for a `!` name the memory doesn't bind, asks the context's
settings for that path's node; a node is a path prefix: `.x` on it is the class's property when the
node is a class, else the child node (`!llm` is llm's own class and the parent of `llm.query`).
`%!goal.list.setting.os%`, `%!llm.cache%`, `%!llm.query.cache%`, `%!build.cache%` all read through it.

**Write / save / remove:** `set %!x%` unchanged (the run layer). `setting.save` (`- save
%!goal.list.setting%`: the instance as read, stored as the actor's row) and `setting.remove` (the row
deleted). `setting.get` / `setting.set` (the key-value table door) go; `Storage` goes — the run layer
is the chain, the saved layer is rows.

**The action-param seam** (generator): step value → the run layer (`module.action.param`,
`module.param`) → the actor's rows (the action's row, then the module's) → `[Default]`. The actor's
rows are read once per actor and held (refreshed by save/remove), so a param read doesn't hit the store.

**CLI flags:** `--test={…}`, `--debug={…}`, `--build={…}`, `--callstack={…}`, `--app={…}` land as the
system actor's run layer under their class's path (`test.setting.timeoutseconds`, …); each owner reads
its instance (test.start reads `test.setting`, not `test.list.Setting`). The owner classes today are
the objects themselves (`app.Build`'s `Files`/`Cache`, `callstack`'s `Timing`…`MaxFrames`, `app.Debug`,
`app`'s `Create`): their settable properties move into `X/setting/this` classes.

**Slices:**
- **7e-2a** the classes by path + the `!` read path + instances built from defaults ← the run layer
  (no rows yet). Fixes `%!build.cache%` (build's own class).
- **7e-2b** rows: save/remove, the actor's rows in the instance and in the seam, user → system, `Storage`
  and `setting.get/set` go.
- **7e-2c** the CLI owners' classes; `test.list.Setting` goes; Executor's `!build.cache` sync goes.
- The builder teaching setting classes to the LLM (properties and defaults) is builder-visible: 7f, with
  its twins and eval.

**Questions:**
1. An action's instance is the catalog action copied with the layers on its rows, saved as
   `Data<action>` — right, rather than an instance of the handler class?
2. Owner setting classes are items through one small base (reflection output, reads itself by
   property) — right?
3. CLI flags as the system actor's run layer under the class path (not a walk onto an object) — right?

**Rulings (plang-40):** 1 yes; 2 yes, base at `app/type/item/setting/this` (the app's own class at
`app/setting/this` derives from it, read `%!app.setting.create%` through `!app`); 3 yes (`--app` too).
Setting classes are kinds of `setting`, rows `{setting, kind: <path>}`. An action's path is a settings
**node** (`%!goal.call.name%` reads the Property row through it; `save %!goal.call%` stores the
Data<action> the node builds). **Bindings collide** with owner paths (`!goal`, `!test`, `!step`,
`!error`, `!event`, `!trace` are memory bindings): owner settings go through the owner's own path under
app — `%!app.goal.list.setting.os%`, the owner answering `.setting` (`ISetting<T>`), a write being the
variable's own last hop. Modules and actions keep the short node form (`%!llm.cache%`,
`%!llm.query.cache%`, `%!build.cache%`). **Known gap:** a module named like a binding (test, error,
event) can't be read by the short form; none has a module-level setting today (Ingi asked whether modules
move to `%!app.module.llm.setting.cache%` too).

## 7e-2a — as built

- `app/type/item/setting/this` — the base: an item (reflection output, `[Out] Path`), a class's path from
  its namespace (a module's own `module.action.<m>.setting` read as `<m>`), or a node's given path.
  `.x` is a declared public settable option, else the asker's settings for `path.x`. Writing an option
  lands in the writer's run layer under `path.option` (and on the instance).
- `app/type/item/setting/kind/this` — a class as a kind of `setting` (name = path, `ClrForm` = the class,
  `Create()` a fresh instance). The type list: `FamilyName` makes a setting subclass a kind of `setting`
  (not a type named `setting` of its own); `Enlist` holds one kind per class.
- `app/type/item/setting/ISetting` — an owner names its class; the item base's navigation (after its
  members) and the list's (before the empty check) answer `.setting` with the asker's instance.
  `goal.list : ISetting<goal.list.setting>`; `goal.list.setting` derives the base (`Os`, `Visibility`
  settable, `[Out]`).
- `actor.setting.Get(path)`: a class → its instance (defaults ← this run's values one level under the
  path, through the convert walk); a module / an action → a node; an action's option (`m.a.p`) → this run's
  (`m.a.p`, then `m.p`), else the catalog property's `Default`; a prefix of a class path → a node; else
  NotFound.
- Root hop: a `!` name the memory doesn't bind reads the asker's settings (bindings first).
- Module classes where a module-level setting is set today: `module/action/llm/setting` (`Cache`),
  `module/action/build/setting` (`Cache`).
- `variable.set`: `set default` checks before the `!` branch (a setting's value counts), and the `!` branch
  (run-layer key) only for a root the memory doesn't bind — `%!app.…%` writes through its hops.
- Executor: `--build`'s cache lands as the system's run value `build.cache` (was a memory name
  `!build.cache` no read reached). **Fix:** `%!build.cache%` resolves (was NotFound since stage 6).
- Tests: `SettingReadTests` (10: class option default / this run / instance, module option falls back to
  the system, build.cache, action option default → module → action, nodes, owner write, NotFound,
  bindings first).
