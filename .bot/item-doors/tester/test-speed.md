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

Ingi's 5 ms holds on paper. Not settled: which of these Ingi takes, and the choice mark's shape.
