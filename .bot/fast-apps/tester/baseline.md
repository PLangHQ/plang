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

## Not measured here

- A class scanned twice in one process (stage 2's check): it needs a count of `GetTypes` calls, which comes with
  stage 2's door.
- A cold start (the CLI's one app): it's out of the plan's scope (C).
- The first unsettled run (load 2 → 37 mid-run) was discarded; the gated rerun is the baseline.
