# Test speed — where a birth's time goes

Tester, 2026-10-20, item-doors at 8378c1b1d (after the kinds commit 925787b46 and fix B 290aad619).
Brief: `.bot/item-doors/architect/briefs/test-speed.md`. Machine: 8 cores, shared with the other bots — the load
is written beside every number.

## 1. A birth, split by part

`new app("/test")`, 100 warm births after 20 warm-up, Debug build. Temporary stopwatch marks in
`Registry.Load`/`Enlist`, `app` ctor and `module.Discover` (reverted; never committed — the profiler is an
[Explicit] test kept out of the tree). Four runs, load (1-min) 4.9 – 11.

| part | ms per birth | what it is |
|---|---|---|
| **E3 choice walk** | **36 – 48** | `GetProperties` on every class (~3,340) looking for `choice<T>`. Finds **29 sets**; making them (`set.For`) is 0.7 ms, holding them + their readers 0.6 ms. The rest is the walk itself |
| **L2a type entries, faceted** | **21 – 32** | every type built a second time holding the list: per type `Alias`/`Owned`/`Internal` statics, `set.For`, `GetMethods` + `GetProperties` with an `[LlmBuilder]` check each — the facts, read for all ~3,280 types |
| A4 module | 6.3 – 9 | 142 action classes: the class walk 1.3 ms, making 142 catalog action objects 5 ms |
| E2 formats + list kind | 6.7 – 8.8 | `IsDefined([Format])` on every class, then bind encode/decode (`MakeGenericMethod`+`Invoke`) and `Activator` per format |
| L1c type entries, raw | 3.9 – 4.7 | first pass of `new type(name, clr, null)` + `Admit` (its `Clash` scans the list per claim) |
| E4 subclass kinds + readers | 3.3 – 3.8 | `FamilyOf` on every class, `Activator` per subclass kind |
| L2b re-add + Admit | 1.7 – 2.0 | |
| E1 name index + kind classes | 1.3 – 1.5 | |
| **L1a GetTypes + L1b NameOf** | **0.3 + 1.2** | reading the classes and their names |
| everything else in the app | ~1 | actor, Code, goal/test/error/…, store, path schemes, console channels |
| `.Testing()` / `DisposeAsync` | 1.1 / 0.0 | |
| **ctor median** | **70 – 104** (min 59 – 67) | **15.3 MB allocated per birth**, ~3 gen0 GCs per birth |

**Reading the classes is ~2 ms. Making objects and walking members is the rest.** The brief's fork ("if
reading the classes dominates → share the facts; if making the objects dominates → make on first ask"): it's
the second.

## 2. How TUnit runs the suites

- Every suite runs TUnit's default: **parallel, adaptive** (`--parallelism-strategy adaptive`, max workers
  auto). No suite sets a limit; 3 test files use `[NotInParallel]`-style attributes. No test class shares an
  app (`[Before(Class|Assembly|TestSession)]`, `ClassDataSource`: 0 files).
- `dev.sh` runs at most 3 suites at once (`DEVSH_PARALLEL=3`), each under `nice -n 10`.

Types suite, 874 tests:

| mode | load before | wall s | CPU s (user+sys) | CPU / wall |
|---|---|---|---|---|
| default (adaptive) | 39 (busy) | 157 | 329 | 2.1 |
| serial (`--maximum-parallel-tests 1`) | 29 (busy) | 122 | 127 | 1.0 |
| default (adaptive) | 4.7 | 39 | 154 | 3.9 |
| serial | 10.5 | 79 | 84 | 1.1 |
| 8 workers | 5.3 | 31 | 139 | 4.5 |

- Quiet machine: parallel is 2 – 2.6× faster in wall time. Busy machine: parallel is *slower* than serial.
- Parallel spends **1.7× the CPU of serial** for the same tests (139 – 154 s vs 84 s) — contention, and at
  15 MB per birth, GC. Cutting the birth's allocation should cut this too.
- Serial, a test costs **94 ms CPU on average; ~70 ms of it is the birth (~75%)**.

## 3. What tests change on an app

Grep over the 703 test `.cs` files (bin/obj excluded); the pattern is beside each count. 4,596 `[Test]`s,
1,258 `new app(...)` sites in 555 files (a field initializer is one birth per test).

| change | files | hits | pattern |
|---|---|---|---|
| goals added / loaded | 58 | 166 | `.goal.list.Add(`, `.goal.Load(` |
| settings set/saved/loaded | 32 | 102 | `Setting.(Set\|Save\|Load\|Remove)(` |
| channels registered / swapped | 68 | 168 | `Channel.Register(`, `.module.channel.Set(` |
| events bound | 4 | 37 | `.on[…]`, `.on.x.Before/After(` (the brief counted 13 with a wider pattern) |
| types / modules registered | 16 | 44 | `.module.Register(`, `.type.list.Add(` |
| `Debug` set | 6 | 11 | `.Debug =` |
| identity | 13 | 38 | `.identity.`, `Identity.(Set\|Create\|Add)` |
| cache / store written | 1 | 1 | `.cache.(Set\|Add)`, `app.store.Set(` |
| files written (disk, not app) | 169 | 527 | `File.WriteAll…/Create/Copy/Move`, `Directory.Create` |
| **any of the above (excl. files)** | **163** | | |
| runs something (memory, callstack) | 267 | | `.Start(`, `Variable.Set(`, `.Run(` |

So a pool would have to undo changes in at least 163 files, and every run changes variable memory and the
callstack (267 files) — the brief's split-lifecycle risk stands.

## 4. The design so far (with Ingi)

Ingi: "plang is all about lazy, so reading all those classes might be a mistake … not sure if it's possible
only when needed"; "if choice is also expensive, maybe it needs to be marked somehow?"; "I think we should be
able to get a new app around 5ms".

The numbers back both: each test keeps a fresh app (isolation whole, no pool, no statics); the birth stops
making what it doesn't need:

| change | saves | owner |
|---|---|---|
| a choice set is **marked** on its class, read like `[Format]` — no property walk | ~40 ms | type system (an attribute: a locked-core addition → architect → Ingi) |
| a type entry's **facts made on first ask**; one pass of entries, not two | ~25 – 30 ms | private to `type` / the type list |
| formats: the `[Format]` attribute read eagerly (MIME/extension index + `Guard`), encode/decode bound on first use | ~5 ms | private to the type list |
| subclass kinds and their readers found when the family is asked | ~3 ms | private to the type list |
| a module's catalog action made on first ask (name → class stays eager) | ~5 ms | private to `module` |
| **left eager** | **~4 – 5 ms** | GetTypes + NameOf + name index + format attributes + the rest of the app |

Ingi's 5 ms holds on paper.

**Ingi's yes (2026-10-20): all four** — mark choice sets; type facts on first ask (one pass of entries);
formats' encode/decode and subclass kinds on first ask (the `[Format]` attribute still read eagerly for the
MIME/extension index and `Guard`); a module's catalog action on first ask (name → class eager). Each test keeps
its own fresh app — no pool, no statics.

Open for the architect: the choice mark is a new attribute on the locked type system — its shape and the
surface re-pin go under a decision. Then a birth is re-profiled against the ~5 ms target (the profiler is kept
outside the tree; it re-applies as temporary marks).

## 5. What was built (tester, 2026-10-20)

Correction to §1: the list holds **~131 types**; ~3,340 is the number of **classes** a birth walks (the "3,280
types" above came from a broken counter).

| commit | what | birth median (load) | CPU / birth | alloc / birth |
|---|---|---|---|---|
| tip before (8378c1b1d + fix B) | — | 90 – 113 ms (~17) | — | 15.5 MB |
| e92111cc4 | a choice set is marked `[Choice]`; PLNG005 at build for an unmarked one | 48 – 51 ms (~17) | — | 9.4 MB |
| dd899b865 | a type's facts read from its class on first ask; one pass of entries | 34 – 37 ms (13 – 16) | — | 5.9 MB |
| 328799fb1 | types found by their class through an index (was a copy + walk per lookup) | 28 – 32 ms (8 – 13) | 34 – 38 ms | 5.1 MB |
| 81159279d | the guard keeps the names and classes; a clash is a lookup (Clash 2.5 → 0.8 ms) | 28 – 30 ms (9 – 11) | 33 – 35 ms | 5.1 MB |

Dropped: **(4)** a module's catalog action on first ask — after the class index it saves ~2 ms and would hold the
actions twice (architect, decision 118). **Subclass kinds on first ask** — measured: of their 3.6 ms, making the
kinds and readers is ~0.6 ms; the rest is two walks of every class (the serializer-reader index 2.0 ms,
`FamilyOf` 1.4 ms). Not worth a change. **(3)** formats binding their own encode/decode moves `kind`'s pinned
constructors: waiting on Ingi (decision 113).

### What's left, on 81159279d (~30 ms, load ~11)

| part | ms | what it is |
|---|---|---|
| formats: bind encode/decode + make the kinds | 6.9 – 7.4 | (3) built after: the bind (~2) is gone |
| `[Format]` + `[Choice]` checked on every class | 4.1 | a class walk |
| module (class walk 1.8, 142 actions 2) | 3.6 – 3.9 | |
| subclass kinds (2 class walks 3.4, making 0.6) | 3.6 | |
| making the 131 entries (their `Alias`/`Owned`/`Internal`/`Offer` statics 3.0) | 3.4 – 3.8 | |
| kind classes (a class walk) | 1.6 | |
| choice sets | 1.6 | |
| `NameOf` on every class | 1.4 | a class walk |
| Clash 0.8, GetTypes 0.4, Guard 0.2, rest of the app 1.2 | 2.6 | |

### The suites, re-timed on 81159279d (quiet machine, load ~2)

| suite | mode | wall s | CPU s | before (§2, load ~5 – 10) |
|---|---|---|---|---|
| Types (876) | default (adaptive) | 14.6 | 55 | 39 wall / 154 CPU |
| Types | serial | 26.8 | 29 | 79 wall / 84 CPU |
| Modules (1,290) | default | 39.8 | 137 | — (dev.sh's ~43 s target, run 3 suites at once) |
| Runtime (1,078) | default | 29.7 | 102 | — |

Serial, a Types test now costs **33 ms CPU** (was 94). The morning's numbers were taken at higher load, so read
the CPU column, which load moves least.

**(3) built (decision 157):** a format binds T's own encode/decode on its first write or read, once per T for
the process; the registry binds nothing. Measured on the same machine, 3 runs × 100 births each side: before
**28 – 31 ms** (CPU 36 – 39), after **26 – 29 ms** (CPU 32 – 36), alloc 5.1 MB both. It saves **~2 ms**, not the
7 I predicted: of the formats' 6.9 ms, the binding was the smaller part; making the format kinds (reading every
`[Format]`, `Activator` per format) is the rest. So a birth is now ~27 ms, not 20 – 23. What is left then is mostly **each app walking the same ~3,340
classes with reflection** — about 10 – 12 ms of the same answers from the same code, every birth. No lazy or
private change removes it.

## 7. §6 built on test-speed (option 1): a birth at ~3.5 ms

**What was built.**
- `app.assembly.@this` (`PLang/app/assembly/this.cs`) holds what plang's assembly says: types (with their
  `Alias`/`OwnedClrTypes`/`Internal`/`Offer` statics), kind classes, formats, `[Choice]` sets, family subclasses
  and actions. Each is read by reflection on first ask, then held.
- `new app(path, …, assembly: code)` is born from a given `code`; an app given none reads its own. This is the one
  surface change: the app constructor's new parameter, re-pinned on this branch.
- The type list (`Registry.Load`/`Enlist`) and the module make each app's own objects from the facts. Kinds,
  formats, choice sets and actions are still new per app, with no reflection.
- `empty.Add` keys the new kind instead of re-keying every held kind on each add. It was 6 ms of a birth (142
  formats, an O(n²) re-key), and the fix helps every birth, shared facts or not.
- Tests: `PLang.Tests/Shared/Session.cs` reads the code once per test process. All 1,275 `new app(…)` sites go
  through `Session.App(…)`, a mechanical rewrite of 554 files.
- `plang --test`: each test's app is born from the runner app's `assembly`.

**Birth** (100 warm births, 3 runs, load ~3):

| | median | CPU / birth | alloc |
|---|---|---|---|
| before §6 (157 tip) | 26 – 29 ms | 32 – 36 ms | 5.1 MB |
| fresh (reads its own facts) | 15 – 16 ms | 17 – 19 ms | 2.3 MB |
| shared facts | **3.4 – 3.7 ms** (min 2.9) | 5 – 7 ms | 1.7 MB |

Split of a shared birth (load ~4): type list 2.5 ms (entries 0.9, formats 0.6, choices 0.4, kind classes 0.2,
subclasses 0.2, guard 0.1), module 1.2 ms (142 catalog actions), channels 0.5, Code 0.3.

**Suites**, each alone (load 12 – 15, so a busy machine):

| suite | wall / CPU now | §5, quiet |
|---|---|---|
| Types | 8.8 s / 24 s | 14.6 / 55 |
| Runtime | 19.1 s / 56 s | 29.7 / 102 |
| Modules | 33.5 s / 75 s | 39.8 / 137 |
| Data | 13.0 s / 31 s | — |
| `plang --test` | 15.6 s / 27 s | 17.3 / 33 (same load, before the runner shared) |

C#: green except Wire's six. plang: 277 / 11, the same 11 by name.

**Open for Ingi.** The holder is option 1 (the app is born holding its code). Its one surface change is the app
constructor's `assembly` parameter. `Session.Code` is a static in test code: one per test process, which is the
test session. The name `assembly` is a placeholder.

## 6. For Ingi: the code's facts, read once per process

The 5 ms needs the class walk to happen once, not per app. Each test still gets a fresh app.

**What is the same for every app in a process** (it comes from the code; the facts):
- the classes the type list holds, each with its name and its identity statics (`Alias`, `Owned`, `Internal`,
  `Offer`);
- the kind classes and which type each is a kind of;
- the `[Format]` classes, their formats and the type that reads them — and their bound encode/decode, which are
  delegates to static methods (`T.Encode`), the same for every app;
- the `[Choice]` classes; the subclass families (class → family, kind) and the reader each ships;
- the action classes: module, action name, class, cacheable.

**What stays per app** (made at its birth from the facts, as now, without reflection):
- each type entry — an item, with its own `on` table (a program binds events on a type);
- each kind object, born holding its owner entry (79) — format kinds, choice sets, subclass kinds, list kinds;
- the reader registry (`type.list.Reader`), and each module's catalog actions (a program binds on them);
- everything else in the app (actors, channels, store, goals) — untouched.

**Who holds the facts** — not a static (Ingi's rule). `app.parent` is not a holder: it is the link to the app that
started this process (a cross-process call), not an object in this one. The options:
1. **The facts are a value the app is born with.** `new app(path, facts)` — the facts are one object (the code's
   facts, say `app.code`'s or a `type.facts`), read from the assemblies by whoever makes apps, and handed to each.
   Production makes one app per process (the CLI) and reads them once. A process that makes many — `plang --test`'s
   child app per test, the C# tests — makes the facts once and hands them to each. The C# tests' 1,258 `new app(…)`
   sites would go through one fixture (a per-session facts object), a mechanical change.
2. **One app makes the others** (a session app): a test session holds one app and asks it for a fresh app
   (`session.App(path)`), which takes the session's facts. Same per-test isolation; the facts' holder is an app.

Either way the birth becomes: make ~131 entries + their kinds + 142 actions from in-memory facts. Estimate:
**3 – 6 ms**.

Not settled: which holder, and whether the facts are a new core member (an `app` constructor parameter or a
`type` member is locked surface — a proposal to the architect, then Ingi).
