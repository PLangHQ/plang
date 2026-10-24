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

## Not measured here

- A class scanned twice in one process (stage 2's check): it needs a count of `GetTypes` calls, which comes with
  stage 2's door.
- A cold start (the CLI's one app): it's out of the plan's scope (C).
- The first unsettled run (load 2 → 37 mid-run) was discarded; the gated rerun is the baseline.
