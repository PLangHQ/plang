# fast-apps — stage 0: the baseline

Measured on fast-apps as cut (`013396728`), 2026-10-24, by plang-tester. Every stage is accepted against these
numbers (`test/plan/fast-apps/start.md`, "What tests can't show"), measured by the same benchmark at the same load.

**Rerun:** `.bot/fast-apps/tester/bench/run.sh <label> [rounds]` (e.g. `run.sh stage1`). It builds, copies
`bench/Bench.cs` into the Types suite for the run (removed after), and runs three rounds back to back. Before each
measurement it waits for the 1-minute load to fall under 3 (`LOAD_MAX`), because the machine is shared. Results
append to `bench/results/<label>.txt`; stage 0's raw lines are `bench/results/stage0.txt`.

Machine: i7-8665U, 4 cores / 8 threads, WSL2. Server GC off (the test host's default).

## The four numbers

| | stage 0 (3 rounds) | load | what it is |
|---|---|---|---|
| **an app's birth** | **Debug 28–32 ms** (median of 100; one round 56 ms at load 5), **Release 27–31 ms**; CPU 31–38 ms; **5.2 MB** allocated per birth + dispose | 2.4–2.9 | `new app.@this(path)`, 20 warm then 100 timed, each disposed before the next |
| **the C# sweep** | **103–136 s** wall (103 and 106 at load 1.5) | 1.5–1.6 | `./dev.sh test --force`: Modules 1,333 / Types 951 / Wire 596 / Data 863 / Generator 191 / Runtime 1,242, all green but Wire's 6 known |
| **`plang --test`** | **17–27 s** wall (17 and 18 in two of three) | 2.6–2.9 | from `test/`, PlangConsole rebuilt first: 294 pass, 1 fail (`BareLiteralJudgedByForm`, known), 3 stale (`plan/snapshot`'s three, unbuilt by design), 1 skipped |
| **a request app × 3,000** | **Release 21–22 ms each (45–48/s), Debug 24–29 ms** (one round 41 ms at load 8); **6.6–6.9 MB** allocated each; **held after a full GC: 0.9 → 45 → 87 → 130 MB** at 0 / 1k / 2k / 3k | 1.5–1.8 | one at a time: an app born (no console), `Condition.test.goal` loaded and run, disposed; held = `GC.GetTotalMemory` after two forced, compacting collections |

The load is the 1-minute average: for the birth and request rows it's read when the measurement ends, and for the
sweep and `plang --test` when it starts.

## What it shows

- **A request app leaks ~43 KB.** The held memory grows by the same 42.5 MB per 1,000 requests in all six runs
  (Debug and Release), and nothing is let go between blocks. `Condition.test.goal` has no `cache for` step, so this
  isn't only §29's step cache with a cached step (73.5 KB). Stage 1 (the step cache disposed) is the first check
  of how much of it is the cache. Stage 1's red test is `RequestAppsLeaveNothingBehindTests`.
- **A birth is 28 ms and 5.2 MB**, Debug and Release alike: the time is reflection and building the world, not JIT
  quality. This matches test-speed's §29 starting point (26–29 ms). Stage 2's target is ~3.5 ms, stage 4's ~0.1 ms.
- **A request costs ~22 ms, and nearly all of it is the birth.** A request allocates 6.7 MB, of which 5.2 MB is the
  birth. The goal's own run is ~1.5 MB.
- **The sweep and `plang --test` are load-bound.** Even after waiting for a load under 3, another session's
  compile can start mid-run (round 1's sweep at 136 s). Compare a stage by its best two of three, at the load
  written beside it.

## Stage 1 — the step cache disposed (`9c0a7d129`), 3 rounds, `bench/results/stage1.txt`

| | stage 0 | **stage 1** | load |
|---|---|---|---|
| a birth | Debug 28–32 ms, 5.2 MB | **Debug 25.7–27.2 ms, Release 26.5–27.3 ms, 5.2 MB** (unchanged) | 1.9–2.8 |
| the C# sweep | 103–136 s | **98–103 s**; Wire's 6 known; round 3 also failed `ErrorInPlay_DuringRecovery_IsTheTimeoutsOwnVerdict` (Modules; once in three, not seen at stage 0 — its message was `Expected "Timeout" but found ""`; not re-run alone yet, to be watched) | 1.3 |
| `plang --test` | 17–27 s | **16.6–17.1 s**, the same 294 pass / 1 fail / 3 stale | 2.4–2.9 |
| a request app × 3,000 | Release 21–22 ms, Debug 24–29 ms; 6.6–6.9 MB each; **held 0.9 → 45 → 87 → 130 MB** | Release 21.6–22.0 ms, Debug 24.1–24.7 ms; 6.6–6.9 MB each; **held 0.9 → 3.2 → 3.8 → 4.4 MB** | 1.3–1.6 |

**The 43 KB a request was the step cache**, on every goal: every app makes its `MemoryCache` at birth (used or not),
and until stage 1 it lived on through its timers. Held memory per request fell from ~43 KB to **~0.55 KB**. Time and
bytes allocated per request did not move (stage 1 frees memory; it doesn't allocate less).

### What still grows: ~0.55 KB a request, and it is let go

Held memory still climbs 3.3 → 3.8 → 4.4 MB over the 3k requests. A heap dump (`dotnet-gcdump`) after 1k and 3k,
diffed by type (count × size, 2,000 requests apart):

| grows by | objects | per request |
|---|---|---|
| `Dictionary<string,object>` + its `Entry[]` | 2,000 each | 0.37 KB |
| `SqliteConnectionStringBuilder` | 3,000 live | 80 B |
| `SqliteConnectionPoolGroup` (+ the pool table's array, 50 KB) | 3,000 live | 48 B + |
| strings, `int[]` | ~4 and 1 a request | ~90 B |

Nothing of plang's own grows (no app, goal, type, call or step object). What grows is **Microsoft.Data.Sqlite's
connection-pool bookkeeping, one pool group per distinct connection string**: every app's in-memory store opens its
own named database (`Mode=Memory;Cache=Shared`, `store/sqlite/this.cs:66`), so every request adds a pool group keyed by
a new string. It **is let go**: after the 3,000 requests, resting 300 s and collecting again, held fell
4.4 → **1.8 MB** (the library retires pools on its own timer, as §29 measured for file databases). So it is bounded
by time, not a leak; at 1,000 requests a second it would be a standing ~0.5 MB a second for the minutes before the
timer. Setting `Pooling=false` on the in-memory string does **not** help (3k: 4.8 MB; tried and reverted, no change to
the tree). I did not look into why.

What would end it, untested: an app that never writes opens no SQLite at all (stage 5's "an in-memory store nothing
has written answers empty without opening SQLite", `store/sqlite/this.cs:59–73`). A request app that only reads
then adds no pool group. Stage 5's rerun of `RequestApps` shows whether that closes it.

## Stage 2 — the door (`d56b3fe22`; the branch tip's code is the same), 3 rounds, `bench/results/stage2.txt`

Measured after the host's symlink fix (no DLL in `bin/` is a link). Each app twice: **root** is `new app.@this(path)`
as today; **child** is `new app.@this(path, actor)` through the internal door, its actor the User of one
server-like app made once (before the first count). A child is born with console channels (the door has no flag);
the root request app is born without, as before.

| | stage 1 (root) | **stage 2 root** | **stage 2 child** | load |
|---|---|---|---|---|
| a birth | Debug 25.7–27.2 ms, 5.2 MB | Debug 25.7–28.0 ms, Release 28.6–31.9 ms, **6.05 MB** | **Debug 13.0–14.6 ms, Release 15.9–16.7 ms, 4.5 MB** | 1.3–2.8 |
| a request app × 3,000 | Release 21.6–22.0 ms, 6.6–6.9 MB; held 0.9 → 3.2 → 3.8 → 4.4 | Release 20.9–21.6 ms, **7.0–7.2 MB**; held 1.0 → 3.4 → 3.9 → 4.5 | **Release 13.0–13.5 ms, 5.9 MB**; Debug 14.8–17.3 ms, 6.2 MB; held 2.5 → 4.0 → 4.6 → 5.1 | 1.2–1.7 |
| the C# sweep | 98–103 s | 104–106 s; Wire's 6 known; **Types 2 and Data 2 red** (below) | — (only Types makes its apps through the door so far) | 1.2–2.7 |
| `plang --test` | 16.6–17.1 s | 16.9–18.5 s: 298 pass, 2 fail, 3 stale | — (the runner still makes root apps) | 2.5–2.8 |

(Two readings were taken at load 11 and 17 and are not used: round 1's Release root request, 39.9 ms, and its Release
child request, 27.7 ms. Both reran at load ~1.3 in rounds 2–3.)

- **A child is born in half the time of a root, and allocates 26 % less**: 13–16 ms against 26–32 ms, 4.5 against
  6.05 MB. A child request runs in 13 ms against 21 ms. **This is not yet the plan's stage 2 target (~3.5 ms):** each
  child still makes its own type list from the parent's facts (the plan says so), and the other 10–13 ms is still being
  spent in the birth. I haven't profiled where; say if you want it.
- **The root got heavier**: a root birth allocates **6.05 MB, up from 5.2 MB** (+0.85 MB), and a root request 7.0–7.4 MB,
  up from 6.6–6.9. Time did not move. This is the cost of the stage 2 code on the path that makes a root app, which the
  command line still takes. I haven't looked for which part.
- **Held memory per request is the same as stage 1** (~0.55–0.6 KB, the sqlite pool bookkeeping above) for root and
  child alike. The child's start is 1.6 MB higher (2.5 against 0.9): the one server-like app is made before the first
  count and stays held.
- **The reds are plan tests written red before their stage**, all named in `test/plan/fast-apps/start.goal`:
  Types `AChildsType_IsItsParentsType` (the child's type list is not the parent's reference),
  `ASharedTypeList_RefusesAnAddition_SayingWhy` (no `TypesShared` yet); Data `AGoalChangedOnDisk_IsReadAgain`,
  `AChild_FindsItsParentsGoal` (goals not yet through the parent); `plang --test`
  `/plan/fast-apps/app/parent/TestAppHasItsRunnerAsParent` (architect decision 18 deferred the in-process parent).
  `BareLiteralJudgedByForm` and Wire's six are the known reds from stage 0. There is no coder
  `baseline-failures.txt` on the branch, so these are matched by name against `start.goal`, not against a baseline.

## Why the root grew: 5.2 → 6.05 MB a birth (decision 25's first ask)

Root-only birth test (`new app.@this(path)`, 20 warm then 100 births, alloc counted by the runtime) built at each commit
between stage 1's close and the tip. Two commits did it, and the door did not:

| commit | alloc per root birth | change |
|---|---|---|
| `9c0a7d129` stage 1 closed | 5,225 KB | — |
| `090847b61` the facts are the type list's own, read once per code | 5,743 KB | **+518 KB** |
| `854dd1fb2` the door, the System born from the caller | 5,745 KB | +2 KB |
| `b67c9b782` a type's kinds looked up without its lock (stage 3 (5)) | 6,057 KB | **+312 KB** |
| `388c667b5` the runner makes each test's app through the door | 6,057 KB | 0 |

**`b67c9b782` — `type/kind/empty/this.cs` `Add`, lines 132–149 on the tip (+312 KB).** To publish the kinds whole, `Add`
now builds a new array for every kind it takes in: `System.Array.FindAll(_kinds, …)` (line 138) makes one array and the
collection expression `[.. …, kind]` a second; the old code did `_kinds.RemoveAll` / `_kinds.Add` in place. A type that
takes k kinds one at a time allocates 2k arrays of growing size: O(k²). Checked directly: the tip with only the old
`kind/empty/this.cs` put back (temporary, reverted, nothing committed) allocates **5,754 KB** a birth, 303 KB less.
(The old `Held` copy under the lock, `ToArray()` on every read, is gone, and the saving is smaller than the cost.)

**`090847b61` — `type/list/Code.cs` `Library`, built eagerly for every class of the assembly (+518 KB).** The architect's
guess holds. Allocation sampling (`dotnet-trace gc-verbose`, 120 births at each commit, per birth, ±~20 KB sampling) names:

| grew by | at | what it is |
|---|---|---|
| **+151 KB** `Mark` | `Code.cs:89` `Marked = Classes.Select(t => new Mark(t))…` | a `Mark` for every class (~3,340), of which a few are kept |
| **+86 KB** closure class | `Code.cs:139–174` `Readers` | the lambda at 174 captures `method` (168), declared in the `foreach` body, so the compiler makes one closure object per class visited, including the classes that `continue` before they reach it |
| +40 KB strings | `Code.cs:100` (Owner) and `:116` (Subclassed) `NameOf(Type)` | a name built for every class, in each of two passes |
| +32 KB | `Class` ctor, `Code[type]` | the declared statics read per class |
| +19 KB | `kind.json` ctor | — |
| +21 KB | `MethodBase[]` in `Enlist` | — |
| ~+100 KB | strings, `RuntimeTypeCache`, attribute records, delegates | spread over the passes above |

(The old `Registry` path allocated ~650 KB a birth in `Enlist`'s per-class lambda and that is gone, so these are the
net of a move, not the whole cost.) The command line makes one root app per process, so it pays this once; a server
that makes root apps, or a test that does, pays it each time. A child that takes its parent's facts doesn't (below).

## The layer — `fast-apps-layer` at `b7c59ecb8` (stage 3 part 1), bench only, `bench/results/layer.txt`

A child's type list is a layer over its parent's; nothing is scanned at birth. The sweep was not run (expected red).

| | stage 2 child | **layer child** | layer root (unchanged) | load |
|---|---|---|---|---|
| a birth | Debug 13.0–14.6 ms, Release 15.9–16.7 ms; 4.5 MB | **Debug 3.3–3.6 ms, Release 3.35–3.5 ms; 1.16–1.17 MB** | Debug 26.0–27.5 ms, Release 27.7–30.5 ms; 6.05 MB | 1.1–2.7 |
| a request app × 3,000 | Release 13.0–13.5 ms; 5.9 MB; held 2.5 → 4.0 → 4.6 → 5.1 | **Release 7.1–7.9 ms; 2.5–2.7 MB; held 2.4–2.5 → 3.7–3.8 → 4.6–4.7 → 5.4–5.5**; Debug 8.4–8.8 ms, 2.76 MB | Release 20.9–21.8 ms; 7.2 MB; held 0.9 → 3.3 → 3.9 → 4.5 | 1.1–1.4 |

- **A child is born at ~3.4 ms and 1.16 MB: the plan's ~3.5 ms is met.** That is a quarter of stage 2's child and an
  eighth of a root. A child request app runs in 7–8 ms (a root: 21 ms) and allocates 2.5–2.7 MB (a root: 7.2 MB).
  Because it is within target, no profile of a child's birth was taken.
- **Held per request rose slightly**: ~0.8 KB a request after the first thousand (3.9 → 4.7 → 5.5 MB) against ~0.6 KB at
  stage 2 (4.0 → 4.6 → 5.2). The sqlite pool bookkeeping above is the same; I did not look for the extra ~0.2 KB.
  Rerun it with `BENCH_REST=300` if the layer lands.
- **The root is the same as stage 2** (6.05 MB, 26–30 ms): the layer doesn't touch a root's birth, so the two causes above
  stay for the command line.

### The layer's REST=300 (decision 27's ask): not all of it is let go — a real leak, ~0.28 KB a request

A child request app on the layer (`b7c59ecb8`), the heap collected after a 300 s rest (`BENCH_REST=300 BENCH_N=…`):

| requests | held, MB, at 0 / each 1k … | after 300 s rest | above the start |
|---|---|---|---|
| 1,000 | 2.5 → 3.9 | **2.8** | +0.3 |
| 3,000 | 2.4 → 3.8 → 4.7 → 5.4 | **3.3** | +0.9 |
| 6,000 | 2.5 → 3.9 → 5.0 → 5.5 → 6.3 → 7.3 → 8.1 | **4.2** | +1.7 |

Most of the growth is let go (the sqlite bookkeeping, as at stage 1), but **what stays grows with the request count,
0.3 / 0.9 / 1.7 MB: ~0.28 KB a request**, not time-bound. At a thousand requests a second that is ~1 GB a day.
Heap dumps after the rest (1k and 3k requests), by type: the 2,000 extra requests left **+4,000 `item.wire.this`
(88 B each = 352 KB) and +4,000 strings (88 KB)**, exactly two of each a request, and nothing else of size.

**What holds them.** The retention path (dump after the rest) is `[static Server]` → the server-like app → `app.type`
→ `type.list` → a type entry → `item.history` → its `List<item>` → the wires. All 6,000 are in **one** history. A scratch
test that walks the type list after 5 and 25 child requests of `Condition.test.goal` finds it: **the shared type entry
`bool` has 10 items in its history after 5 requests and 50 after 25, each a wire of type `type`**.

**The line.** `type/item/source.cs:239`, `item.history.Add(this);` at the end of `Value`: a `.pr` slice that names a type
(`"bool"`) reads to the type entry the list holds, and the source is added to that entry's history — "the source rides
the materialized value's history". With the types shared by the layer, the entry is every app's, so each request's
slices pile onto it. **Checked by mutation** (temporary, reverted, nothing committed): with that one line commented out,
25 requests leave no history on any shared type entry. Lines 193, 210 and 220 of the same file do the same for the other
read paths; I haven't seen them hit.

- It is new with sharing: at stage 2 each child makes its own type entries, which die with it.
- **Likely a race as well, not tested:** `history.Add` is `if (!_list.Contains(prior)) _list.Add(prior)` on a plain
  `List<>` (`type/item/history/this.cs`), and the entry is now shared by concurrent requests.
- Any shared object that takes `history.Add` has the same shape; `bool` is only what this goal reads. A goal with other
  typed slots grows their entries.
- The root request app at stage 1 also kept 0.9 MB after a rest (3k requests, 4.4 → 1.8 against a start of 0.9). A root
  shares no type entry, so this isn't its cause; I haven't found that one.

## Stage 3 complete — the tip `94acb909b` (decision 32), 3 rounds, `bench/results/stage3.txt`

The type list, the module registry and goals are shared through the parent; the hold and the history fix are in. Root is
`new app.@this(path)`, child is `new app.@this(path, actor)`.

| | stage 0 | stage 2 child | layer child | **stage 3 child** | **stage 3 root** | load |
|---|---|---|---|---|---|---|
| a birth | 27–32 ms, 5.2 MB | 13–16 ms, 4.5 MB | 3.3–3.6 ms, 1.16 MB | **Debug 0.50–0.56 ms, Release 0.46–0.52 ms; 646–654 KB** | Debug 27.6–29.5 ms, Release 27.1–30.8 ms; 6.05–6.07 MB | 1.2–3.0 |
| a request app × 3,000 | Release 21–22 ms, 6.6–6.9 MB | Release 13.0–13.5 ms, 5.9 MB | Release 7.1–7.9 ms, 2.5–2.7 MB | **Release 3.7–4.0 ms, 1.62 MB; Debug 4.2–4.5 ms, 1.70 MB**; held 2.4–2.5 → 3.5–3.6 → 4.1–4.2 → 4.6–4.8 | Release 21.2–22.0 ms, 7.35 MB; Debug 23.3–25.6 ms, 7.40 MB; held 0.9 → 3.3–3.4 → 3.9–4.0 → 4.5–4.6 | 1.2–2.6 |
| the C# sweep | 103–136 s | 104–106 s | — | **44.5–64.6 s** (64.6 was the first round after the build; 44.5 and 48.4 after), all green but Wire's 6 known: Modules 1,344, Types 960, Data 871, Generator 191, Runtime 1,242 | | 1.3–2.3 |
| `plang --test` | 17–27 s | 17–18 s | — | **13.5–13.9 s**: 298 pass, 1 fail (`BareLiteralJudgedByForm`, known), 3 stale, 1 skipped | | 2.5–3.0 |

(One reading, round 2's Release root request, 46.1 ms at load 12, is not used; it reran at 21.2 ms at load 1.2.)

- **A child is born in half a millisecond, and a child request app runs in ~4 ms.** Against stage 0: a birth 28 ms → 0.5 ms
  (**55×**), a request app 22 ms → 3.9 ms (**5.6×**) and 6.7 MB → 1.6 MB (**4×**), the sweep **1.6–3×** (103–136 s
  against 44.5–64.6 s; 2.3–3× leaving out the slow first round), `plang --test` **1.2–2×** (17–27 s against 13.5–13.9 s). The plan's targets were ~3.5 ms (stage 2) and ~0.1 ms (stage 4): the birth is already under the first and
  within 5× of the second, with stage 4 (a birth makes only what's asked for) still to come.
- **`plang --test` went from 2 fails to 1 because `TestAppHasItsRunnerAsParent` is gone, not because it passes.** The
  coder's `6a7e5a517` (decision 18: the in-process parent isn't built) removed it from the branch. An earlier line
  here said it passed; that was wrong. See "The parent in a test's app" below.
- **The root is unchanged** at 6.05 MB and 27–30 ms a birth (the coder's two allocation fixes are queued), and its
  request app allocates a little more than at stage 2 (7.35 against 7.0–7.2 MB Release; I have not looked for it).

### REST=300 on a child, 6,000 requests: flat now

Held MB at 0 / each 1k / after a 300 s rest: 2.5 → 3.6 → 4.2 → 4.8 → 5.3 → 6.0 → 6.4 → **2.7**. After the history fix the
rest leaves **+0.2 MB above the start, against +1.7 MB for the same 6,000 requests at the layer** (0.28 KB a request). What
the 6,000 requests add while they run (~0.5 KB each, the sqlite bookkeeping) is let go by the timer; nothing is left that
grows with the count.

### The root's 0.9 MB kept after a rest: a one-time cost, not a leak

Root request app, rested 300 s, collected: **1,000 requests leave 1.7 MB and 3,000 leave 1.8 MB**, against 0.9 MB at the
start. The residue does not grow with the request count (+0.8 at 1k, +0.9 at 3k), so it is not kept per request. Heap
dumps taken after the rest at 1k and 3k: 2,503,009 and 2,526,033 bytes reachable, **+23 KB over the 2,000 extra requests**,
all of it one array (the sqlite pool table, 32 KB); no other type grew. What the process holds after the first requests:

| held | bytes | what it is |
|---|---|---|
| `ConcurrentDictionary<Type, Identity>`: 3,263 nodes + its 140 KB bucket array | ~375 KB | `type/item/this.cs:515`, `_identities`: a process-wide static cache, one entry per class, filled on first use |
| strings (9,187) and `RuntimeType` (4,288) | ~375 KB | names and the reflection objects of the classes read once |
| TUnit's test-source queue (164 segments) | ~126 KB | the test host's own, not plang's |
| a few arrays (`int[]` 98 KB, `ulong[]` 66 KB), `OptimizedInboxTextEncoder`, method infos | ~300 KB | runtime and library tables |

The cache at line 515 is a static in production code, filled once and bounded by the number of classes, so it is not
growth; I note it because the plan says "no static in production" for the code's facts.

## The root after the allocation fixes — the tip `783ad353f` (code `a2ae81cb1`), `bench/results/stage3b.txt`

Root only (`BENCH_ONLY=1 BENCH_MODES=root`), 3 rounds, load 1.1–2.7. The two fixes found at `15e96ecc8` are in.

| | stage 0 | stage 3 root | **after the fixes** | load |
|---|---|---|---|---|
| a birth | 27–32 ms, 5.2 MB | Debug 27.6–29.5 ms, Release 27.1–30.8 ms; 6.05 MB | **Debug 18.6–19.8 ms, Release 19.2–20.5 ms; 2.89 MB** | 1.2–2.7 |
| a request app × 3,000 | Release 21–22 ms, 6.6–6.9 MB | Release 21.2–22.0 ms, 7.35 MB; Debug 23.3–25.6 ms, 7.40 MB | **Release 17.8–18.2 ms, 4.25 MB; Debug 20.2–20.9 ms, 4.41 MB**; held 0.9 → 3.4 → 3.9 → 4.5 (unchanged) | 1.1–1.6 |

- **A root birth allocates 2.89 MB: less than half of what it did (6.05 MB) and 45 % less than stage 0 (5.2 MB).** It is
  also 30 % faster (about 19 ms, from 27–30). A root request app allocates 4.25 MB, down from 7.35 MB (stage 0: 6.7 MB)
  and runs in 18 ms, from 21–22.
- **The 7.35 MB against stage 2's 7.0–7.2 is moot now**: the root is 3 MB under both. I did not look for what made it
  0.15 MB higher.
- The child's rows are unchanged by this (child births are 0.46–0.56 ms, 646–654 KB at `94acb909b`); I did not rerun them.

## The parent in a test's app

- `TestAppHasItsRunnerAsParent` (`test/plan/fast-apps/app/parent/`) is not in my tree, tracked or untracked, and
  `start.goal` line 20 says it went to the backlog with decision 18. My stage 2 run (`d56b3fe22`) had it red, and the
  stage 3 run (`94acb909b`) no longer has it.
- **`%!app.parent%` in a test's app today is false, and nothing links it.** Checked with a scratch test (not committed)
  on `783ad353f`, reading `%!app.parent%` the way a step does: in a root app and in a child made through the door,
  `app.parent` is an `app.parent.this` whose `IsTruthy()` is false and whose `Link` is not set. `Link` is set in
  two places only, `module/screen/type/screen/display/this.cs:73` (PlangOS's screen) and
  `module/terminal/type/process/this.cs:206` (a process this app started). So it is not truthy with nothing linking it:
  no bug.

## Not measured here

- A class scanned twice in one process (stage 2's check): it needs a count of `GetTypes` calls, which comes with
  stage 2's door.
- A cold start (the CLI's one app): it's out of the plan's scope (C).
- The first unsettled run (load 2 → 37 mid-run) was discarded; the gated rerun is the baseline.
