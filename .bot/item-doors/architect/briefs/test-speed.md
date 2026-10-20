# Test speed: brief for the tester

Ingi (2026-10-20): "tests take too long to run. A big part of it is that for each test there is a new app, costing about 100ms, so in regards to solve that I think we need to start to look into pool/rent/etc solutions." Then: "let's get another bot to do this, tester for example … let's move this discussion to him." The discussion is yours now, with Ingi. Nothing is decided.

## The problem, in numbers

- The C# suites make **1,244 apps** (`new global::app.@this(...)`) across **543 test files**, one or more per test. No test file shares an app: none uses `[Before(Class)]`, `[Before(Assembly)]` or a `ClassDataSource`. The most common roots are `"/app"` (418) and `"/test"` (279).
- The coder measured on item-doors (decision 95 in `.bot/item-doors/architect/summary.md`): **a birth is ~150 ms warm, about 90% of the suites' CPU, and `Registry.Enlist` is ~110 ms of that.**
- Fix B (Ingi's yes) reads the assembly's classes once in Enlist instead of four times and finds types through a dictionary. It moved the warm median 214 → 209 ms and the minimum 127 → 90 ms. The cost isn't how many times the classes are walked.
- Earlier, Ingi declined a process-wide scan cache (a static) and a cloned registry: "let's live with the slowness". He has now reopened it.

## What a birth does (`PLang/app/type/list/Registry.cs`, run per app)

- `Load` (`:90–111`): every class in the assembly becomes a type entry; then every entry is built again holding the list (`:100–104`); then `Enlist` and `Guard`.
- `Enlist` (`:288–358`): every kind (`kind.Every`), every `[Format]` class (one `Activator` per format), every public property of every class (looking for `choice<T>` sets, a reader per set through `Activator`), every subclass kind, and a search for each subclass's serializer reader.
- All of this comes from the code, so it's the same answer for every app in the process. What is per app: each type entry's `on` event table, and the kinds, which are born knowing their owner (79).

## The architect's position (to weigh with Ingi; not settled)

**A pool** (keep apps, reset them between tests) has to undo everything a test changes on an app. A grep of the test files finds goals added (52 files), settings set (27), channels swapped or captured (95), events bound on types (13), types or modules registered (15), `Debug` set (6), files written (37), plus identity, cache and kept items. Every new piece of app state would also have to remember to reset: a split lifecycle across the whole app. A missed piece fails silently, so a test passes or fails depending on which test used the app before it. That trades slow tests for flaky ones.

**Rent what the code declares, not the app.** Each test still gets a fresh app, so isolation stays whole; only the part that is the same for every app stops being recomputed. Which way depends on where the 110 ms goes:
- **If reading the classes dominates:** read the facts once and hand them to each new app. To avoid a static, they're held by an object: `app.parent` already exists for "the app that started this one", or a session app. Each entry's per-app state (the `on` table, kinds with their owner) has to move off the entries or be copied. Events are locked, so that part is Ingi's.
- **If making the objects dominates:** build an entry the first time something asks for it. A test touches maybe 20 of several hundred types. This stays private to the type list.

**Check first:** whether TUnit already runs the suites in parallel, and with how many workers. Wall time is CPU time divided by the workers, so a serial suite may be the cheaper lever.

## Rules that hold

- No statics, except constants (Ingi).
- The core is locked (`CLAUDE.md` "The locked core"): the type list's surface, events and `app`'s members. An addition there goes to the architect, then to Ingi.
- No feature flags and no backward compatibility.
- Test isolation isn't traded for speed without Ingi's yes.
- Work from the item-doors tip after the coder's kinds and fix B push, in your own worktree. Your output goes in `.bot/item-doors/tester/`.

## As steps

```
Measure where a birth's time goes, then talk it through with Ingi
Start
- profile `new app("/test")` 100 times, warm, on the item-doors tip: split Registry.Load into reading the classes (GetTypes, GetProperties, attributes) and making the objects (type entries, kinds, Activator, readers), and the rest of a birth by member
- read how TUnit runs each suite: parallel or not, how many at once, wall time against CPU time
- confirm the count of what tests change on an app (goals, settings, channels, events, registrations, files, Debug); the numbers above are a grep
- bring the numbers to Ingi and weigh with him: a pool, the code's facts shared, entries made on first ask
LIMITS: no production change before Ingi picks a design. Profiling code stays out of commits, or goes in a test project as an [Explicit] test
DONE WHEN: a table in .bot/item-doors/tester/test-speed.md: birth ms split by part; parallel or not per suite; app changes per kind
```
