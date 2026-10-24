# fast-apps

## Why

Every `new app.@this(...)` builds the whole world again: it scans every class of the code for the type list (`type/list/Registry.cs:90–99`, `assembly.GetTypes()`) and again for the module catalog (`module/this.cs:112`), makes every part whether it's asked for or not (`app/this.cs:286–364`), and reads every goal's `.pr` again (`goal/list/this.cs:100`, `goal.@this.Load(at, App)`). A test pays this once per test, and a webserver will pay it once per request (item-doors 317a: each request is a new app whose System is the server's actor). On test-speed the tester measured that making an app is about 75 % of a test's CPU (`.bot/test-speed/tester/test-speed.md` §2). With its changes, the C# sweep went from 137–215 s to 29–31 s and `plang --test` from 24–31 s to 5.0–5.4 s (§21).

test-speed was the first attempt, with the locked core open and the code allowed to be bad (item-doors 159). Nothing merges from it. This branch builds what it learned, clean, on runtime2, in the shape the four decisions below give it. The tester's map of it is `.bot/fast-apps/tester/map.md`; §N is test-speed's write-up.

This is the fast-apps half of the snapshot plan ("Frozen parts and children", `test/plan/snapshot/start.md`), done first. One of that plan's decisions changes here: a child doesn't copy a part at its first change (decision 3 below).

## Decisions (Ingi, 2026-10-23/24; the log is `.bot/fast-apps/architect/summary.md`, 3–7)

1. **A new app is made from a parent.** It takes what's the same for every app made from the same code (the code's facts, the type list and its actions, the loaded goals) and makes the rest itself: its folder, memory, channels, store, the settings' run layer and its bindings. (3)
2. **Its System is born from the parent's actor that calls.** The door is handed the calling actor, and the parent is that actor's app (`actor/this.cs:88`). The System is a new actor: it takes the caller's identity (`:101`) and its cancellation is linked to the caller's token (`:117`), so it stops when the caller stops. Its context, channels, permission and settings are its own (an actor holds its app's context, channels and settings' root, `:108`, `:120`, `:122`, so it can't be another app's System). (3, 4)
3. **`app.parent` keeps its word:** the app whose actor made this one. Across processes it is today's link (`parent/this.cs:25`); one parent at most. The in-process parent is not built in this branch (Ingi, 18); it's built with its first user, with PlangOS's end of a call folded in (backlog 9); until then a test's `%!app.parent%` stays false. (4, 18)
4. **Shared whole, or the app's own, decided at birth.** A child takes its parent's type list and its actions whole; a child whose program loads code (`code.load`, `module/code/this.Load.cs:19`) isn't made from the same code, so it's born with its own list, its own actions and its own goals (a goal holds its app's type list, `goal/this.cs:344`). Never copied at the first change: the tester tried it (§23), and values made before the copy held the parent's types while values made after held the copy's, so `type == type` was false for one type inside one app. A shared list refuses an addition with an error that says why (`TypesShared`). (5)
5. **The scope** (6): the five stages below. Left for later: C (the generator writes the facts; cold start only), D (157, approved, a small win after A), §19–§22's trims, and large data (§25–§27).
6. **The proof measures a request app too, not only the suites** (7): a request app's size at birth, and memory flat over a few thousand requests.

## The shape (the coder owns the code; this is the intent)

- **The door.** `new app.@this(path, actor)`: the folder, and the actor that calls. Its parent is `actor.App`; nothing else is passed. The command line has no caller in its process, and its app is made as today (`new app.@this(path)`), reading the code's facts itself.
- **Where parents come from.** `plang --test`: the runner hands the actor that runs the test (`test/list/this.cs:178`, `context.Actor`). The C# tests: one parent per test process, which every test's app is made from (605 test files make an app today; the change is mechanical). A webserver (later): the server's actor that takes the request.
- **The code's facts** are read once, by the first app made from the code, and every app made from it takes them through its parent. No static in production. **They are the type list's own, private, with no new name on any surface** (Ingi, 9): a child that shares the list shares them, and a child born with its own list builds it from its parent's list's facts. test-speed's holder (`assembly/this.cs`: each fact read once under one gate, the generator's lists read when present) is the code to learn from; its name and its place on the app don't carry over.
- **Shared, or the program's own.** Whether a program adds to the code is its actions' own answer: the action says it, and the test or request reaches the answer by navigating its goals. Never a name compare in the runner.
- **What still grows on a shared list** is the same answer for every app from the same code: a kind asked for by name is made once on its type (`type/list/this.cs:179`, `kind.Coin`), and the full types are kept as they're made (`:181`). The kind lookup reads without the type's lock (§30), so sharing it adds no lock across apps.
- **A binding on a shared object is the binding app's own.** A goal, step, action or type entry that apps share keeps no binding an app makes while it runs: the binding is held on the binding actor's context (the System's, for an app-wide one), and it goes with its app (Ingi's idea, §19). What a program's own clauses bind when it's read stays the program's.
- **Goals through the parent.** A goal the parent has read is the child's: the child's list finds it there, and the `.pr`'s modified time and size keep it fresh. A goal changed on disk is read again on its next use.
- **A birth makes only what's asked for** (B): modules and their actions, the code providers, channel settings, stream buffers, `Data.Properties`, an actor's and a context's parts, the store's file, system variables, the memory's table, the app's id and start. Path's schemes become kind classes instead of being added at every birth (`app/this.cs:326–328`).
- **The run path** (§18): a list's positional reads (`list[i]`, `CountRaw`) read its snapshot, not its lock (`type/item/list/this.cs:215`, `:270`); an in-memory store that nothing has written answers empty without opening SQLite (`store/sqlite/this.cs:59–73`); class facts (choice sets, action properties, setting paths and defaults) are read once per code.
- **The step cache** belongs to its app and is disposed with it (`app/this.cs:174` makes a `Memory` holding a `MemoryCache`, `module/cache/Memory.cs:11`; `DisposeAsync`, `app/this.cs:580–594`, never disposes it).

## Stages

0. **Measure** (tester): on fast-apps as cut, an app's birth (time and bytes), the C# sweep, `plang --test`, and a request app born 3,000 times (time, bytes, memory after). The baseline every stage is measured against.
1. **The step cache is disposed** (coder). Independent of the rest; a leak (§29: 73.5 KB kept per request with `cache for …`, 223 MB after 3k requests).
2. **The door** (coder): `new app.@this(path, actor)`, internal (Ingi, 17); the System born from the caller; the code's facts read once and taken through the parent; the runner and the C# tests make their apps through it. Each child still makes its own type list, from the parent's facts (A: birth 26–29 ms → ~3.5 ms).
3. **What's the same is shared** (coder): the type list and its actions whole, or the program's own when it adds code; goals through the parent; a binding on a shared object held on the binding context; the kind lookup without the type's lock.
4. **A birth makes only what's asked for** (coder): B, without §10's entries made on first ask (a shared list makes its entries once per code; they aren't needed). Birth ~3.5 ms → ~0.1 ms.
5. **The run path** (coder): the list's snapshot reads, the empty store, class facts once. `plang --test` ~8 → ~5 s.

Each locked item comes to Ingi with its code before it's built (decision 5). By stage: **2**: none (the door is internal, 17; the in-process parent deferred, 18). **3**: type (a shared list's refusal), events (a binding held on the binding context). **4**: module (`module.Action` as `list<action>`), type (`Alias`/`Internal` without `init`; path's scheme kind classes), call (how the call list is made), data (`Properties` made when asked). **5**: private, by the surface golden.

## Not carried over from test-speed

- The `assembly` and `shareTypes` constructor parameters: the door takes the calling actor, and the parent decides what's shared (`shareTypes` was a bool that forked the birth).
- `Loads` (`origin/test-speed test/list/this.cs:167`): a static on the runner, its name a plural verb, a name compare in two nested loops (Ingi: "that is plural and static = obpv").
- The hidden goal reader app (`goal/program/this.cs`): the parent's goals are the shared goals.
- The 1-second freshness window (3d90ec3f2): a goal changed within a second read stale; the stat keeps it fresh.
- §10's entries made on first ask, and the `Fill()` hook on the list base for one subclass.
- The test `Session` static holding the code's facts: the C# tests hold one parent app per test process instead (a test-code static, the test process's root, as the CLI's root app is Executor's local; architect, 11).
- `held`'s shape (`origin/test-speed event/held/this.cs`): the idea stays whole, but `IsShared` is a static type switch over goal, step, action and type (a **fork**: each shared item answers for itself), and `On`/`Own` are static helpers with a switch over levels (**stray helper**: they belong to the context's hold).
- §14's call-list makers (closures standing in for a reference) and §15's explicit "system variables last" in `GetAll`: each gets a second look before it's built.
- "No stat for a path with one place" (§20): it changes what an `exists` costs; out of scope.

## Stays

- What a child makes itself (decision 1), and today's birth for the command line's one app.
- `app.parent`'s cross-process link and its `call goal X in %!app.parent%` (`parent/this.cs:48`).
- Copy on write where the snapshot plan uses it for its other A parts: the settings' run layer and code providers.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests. A C# test is named in it as a comment, with its file.
- At each stage's end its tests are built and run. A stage is accepted when its tests are green, the checks hold, and the architect's OBP review is clean.
- **Every test fails without its change.** A guard (sharing must not leak) fails under its mutation: drop the change and the guard goes red.
- The plang in start.goal says what is wanted; each test is written in real plang that builds, red before its stage, keeping what it proves.

## What tests can't show (the tester measures, against stage 0)

- **Every stage:** an app's birth (time, bytes), the C# sweep, `plang --test`, a request app born 3,000 times (memory flat). Back to back against stage 0, at the same load.
- **Stage 2:** no class is scanned twice in one process (the second birth reads no `GetTypes`).
- **Every stage:** the gate by name; PLNG001/002 at 0; the surface golden moved only with its decision.

## OBP validation

| surface | plang path | C# path | file path | checks |
|---|---|---|---|---|
| the parent | `%!app.parent%` | `app.parent` | `PLang/app/parent/this.cs` | the app whose actor made this one; across processes the link; one at most; the in-process parent deferred (18) |
| the child's System | `%!app.actor.list.system%` | `actor.list.System` | `PLang/app/actor/list/this.cs` | born from the calling actor: its identity, its cancellation; its own context, channels, permission, settings |
| the code's facts | — | private to `type.list` | `PLang/app/type/list/` | read once per code; reached through the parent; no static; no new name on a surface |
| the type list | `%!app.type.list%` | `type.list` | `PLang/app/type/list/this.cs` | shared whole or the app's own, decided at birth; a shared list refuses an addition, saying why |
| adds to the code | — | the action's own answer | the action's file | no name compare; reached by navigating the program's goals |
| a binding on a shared object | (`on …` steps) | held on the binding actor's context | coder places, under `PLang/app/event/` | never on the shared object; no static type switch |
| the loaded goals | `%!app.goal.list%` | `goal.list` | `PLang/app/goal/list/this.cs` | the parent's goal, kept fresh by its `.pr`'s stat; no reader app, no time window |
| the step cache | — | `app.Cache` | `PLang/app/module/cache/Memory.cs` | disposed with its app |
