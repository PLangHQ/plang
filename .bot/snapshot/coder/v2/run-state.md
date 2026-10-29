# snapshot — the run's state the walk doesn't reach

What a run holds in memory beyond what `snapshot.save` walks today (the app's mode and kept, `Code`'s providers, both actors'
`Variable` and `call`). Read from the code on 2026-10-26 (branch `snapshot`, with 1b's held work). For each piece: its owner
and file:line; whether it is **state** (made by the run, lost on resume) or **rebuilt** (made again from the app's files,
the stores on disk, or the command line); and whether a resumed run needs it.

## State a resumed run needs (lost today)

| # | what | owner, file:line | why a resumed run needs it |
|---|---|---|---|
| 1 | **run-time event bindings** (`on … call …`) | `actor.on`, `actor/this.cs:69`; held in `event/on/list/this.cs:15` (`_held`, item → its own events) | every `on` a program bound before the save stops firing after resume |
| 2 | **"y" permission grants** (one-time yes) | `actor.Permission`, `actor/permission/this.cs:29` (`_once`) | a resumed run asks again for what was already allowed (a resumed ask may block on a prompt). `_standing` (`:28`) is rebuilt from the role (`:36`); "a" grants are in the settings store (rebuilt) |
| 3 | **this run's settings** (`set %!x%`, a setting action's write, the CLI flags) | each actor's layer, `actor/setting/this.cs:19` (`_values`); each context's layer, `actor/context/this.cs:125–129` (`_setting`, goal-local values) | a resumed run reads the defaults/saved rows instead of what the run set. The saved rows (`/.data/setting/data.sqlite`) are rebuilt; the CLI flags are the new process's |
| 4 | **kept members on the other owners** (`set %!x.y% = …`) | actor `actor/this.cs:20`; goal `goal/this.Item.cs:15`; step `goal/step/this.Item.cs:10`; module `module/this.cs:17` (the app's and each frame's are stored) | `%!goal.retries%`-style members vanish. Goal/step/module are program objects the app shares, so their kept lists are run state on shared objects (see "Questions") |
| 5 | **module statics** (`IStatic`) | the context's bag, `actor/context/this.cs:19` (`_data`), reached by `GetModuleStatic` (`:261`); the one user is the timer: `module/timer/start.cs:19–20`, `end.cs:20–31` | a `timer.start` before the save and `timer.end` after it fails ("no timer"). The values are CLR objects (`TimerEntry`), so they have no written form |
| 6 | **channels set at run time** (`set output channel to goal X`) | `actor.Channel`, `channel/list/this.cs:147` (`Register`), `:156` (`Set`), `:173` (`Remove`); action `module/channel/set.cs` | a resumed run writes to the default channels, not the ones the program chose. The default channels and their streams are rebuilt |
| 7 | **modules added at run time** (`module.add`) | `module/module/add.cs:10` → `Code.Load` + `module.Discover` (`module/this.cs:142`) | `Code`'s part stores only ICode **provider** registrations (`module/code/this.Snapshot.cs`), not the modules a DLL brought, so their actions are unknown after resume. The same holds for a `code.load` whose DLL brings only plang types (no providers): its types are joined to the app but nothing records the DLL |
| 8 | **a timeout in flight** (`timeout.after` modifier) | the context's cancellation stack, `actor/context/this.cs:67` | a resumed run inside a timed call has no deadline |
| 9 | **the run's error audit** (`%!app.error.list%`) | `call.list.Audit`, `call/list/this.cs:100` | every error the run met; a program that reads it after resume sees only errors since. History rather than state, but plang-visible |

## State that can't be stored as it is (a live resource held as a value)

| # | what | owner, file:line | note |
|---|---|---|---|
| 10 | **a browser, a window, a screen** a program opened | values in variables: `module/browser/start.cs`, `module/window/open.cs`, `module/screen/listen.cs` (a listening display, `module/screen/type/screen/display/code/Display.cs`) | the variables part stores the value; a browser writes its url/width/height (`module/browser/type/browser/this.cs:38–44`), so a resumed run gets a value, not a live process. Needs a rule: reopen, refuse the save, or mark it closed |
| 11 | **open streams** (a channel's stream, a file being read) | the channels; the actions' locals | external resources; never state a snapshot can hold. Rebuilt (channels) or lost mid-action (an action is re-run from its start) |

## Refused today

| # | what | owner, file:line | today |
|---|---|---|---|
| 12 | **running tasks** (a parallel branch) | `actor.Task`, `task/list/this.cs:13` (`_running`) | a save inside one is SnapshotInTask; a save while one runs is SnapshotWhileTaskRuns (1b) |

## Rebuilt from the app's files, the stores or the command line (not state)

| what | owner, file:line | rebuilt from |
|---|---|---|
| goals, steps, actions | `app.goal`, `this.cs:167` | the `.pr` files (a frame finds its goal by address) |
| types (the app's and DLLs') | `app.type`, `this.cs:250` | the assembly; a DLL's through `Code`'s start (providers only, see 7) |
| the module catalog | `app.module`, `this.cs:153` | the assembly and `os/system/modules` (run-time additions: see 7) |
| the cache | `app.Cache`, `this.cs:181` (`module.cache.Memory`) | nothing: a cache may miss by contract, so a fresh one is right |
| the app's store (data) | `app.store`, `this.cs:190` | its database on disk, which outlives the run. **Except** while testing, where stores are in memory |
| saved settings rows, "a" grants | the settings store, `actor/setting/this.cs:47–50` | `/.data/setting/data.sqlite` |
| the system's identity | `actor.Identity`, `actor/this.cs:117`, set by `module/identity/code/Default.cs:31,66,137` | the identity store. A user's identity is set per request by the HTTP/signing layer: a request's, not the run's |
| debug | `app.Debug`, `this.cs:197` | the command line |
| the test session | `app.test.list.Session`, `test/list/this.cs:32` | the mode is stored and reopens a session; the results so far are not kept (only matters for a snapshot taken mid-test-run) |
| a context's trace id | `actor/context/this.cs:32` | new per context (diagnostic grouping) |
| Data wrappers | `actor/context/this.cs:270` (`_wrapperCache`) | a cache |
| the call stack's diff stream, a frame's diffs and timing | `call/list/this.cs` (`_streamDiffs`), `call/this.cs` | history, not state (ruled in 1b) |

## Unused at run time

- `app.KeepAlive` (`this.cs:291`, `keepalive/this.cs:14`): nothing calls `Add`.
- `app.Services` (`this.cs:275`, `service/list/this.cs:23`): nothing in PLang calls `New`.

## Carried on values the walk already stores (to prove through text, not new parts)

- an llm conversation rides on the answer's Data (`module/llm/type/conversation/this.cs:15`), its `Messages` property: stored with the variable that holds the answer, **if** a Data's properties survive text (not yet tested).
- the event running in a frame: 1b stores it; through text it waits on the goal-reference question (the item, the owner).
- `_runningBindings` (`actor/context/this.cs:285`, which binding runs in this flow): flow-scoped; a run resumed inside a bound call can fire that binding inside itself once. Small; follows the event.

## Questions this raises (for the architect, then Ingi)

1. 1–9: each is its owner's part (`actor` writes `on`, `Permission`, its settings layer and kept; the context its settings layer, statics and deadlines; `channel.list` its run-time channels; `Code` the modules and type-only DLLs it loaded). Which are in "everything"? 9 (the audit) and 8 (a deadline) are the borderline ones.
2. 4: kept on a goal/step/module is run state written onto objects the app shares (CLAUDE.md: "a run never writes an object its app shares"). Store them under the program reference (the goal's address), or move them off the shared object first?
3. 5: module statics hold CLR objects; storing them needs the statics to be plang values first.
4. 10: a live resource held in a variable: reopen on resume, refuse the save, or come back closed?
