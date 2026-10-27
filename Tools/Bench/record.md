# Speed record

What the runtime costs, one row per run of `Tools/Bench/run.sh`, newest first. Run it at every review and add the row:
a number outside its band below is a change, not noise.

```
Tools/Bench/run.sh                    # everything; ~5 min. Choose the parts from the diff (see run.sh's header); a package-only diff is BENCH_SKIP="birth request"
BENCH_REST=300 Tools/Bench/run.sh     # + the two 300 s rests: only when the diff touches the store, its pool, or what a request app holds (the "after the rest" held MB is 0 → the last reading otherwise)
BENCH_SKIP="build sweep" Tools/Bench/run.sh        # leave parts out: build sweep ptest birth request
BENCH_LABEL=my-branch-tip Tools/Bench/run.sh
```

Each measurement waits for the 1-minute load to fall under 3, and the row's **load** is the highest load seen at the
start or the end of any measurement. A row with load over 3 means a wait timed out or a neighbour's build started
mid-run: read it against the bands with that in mind (one Debug request at load 13.7 took 40 ms where the same one at
load 1.5 took 20; a clean build at end load 2.9 took 168 s once, 49–54 s otherwise: the 1-minute load lags, so even a
low load beside a slow row means "run it again").

## Noise bands

How far two runs at the same load differ, from the runs on `fast-apps` (stage 3b, stage 5, three rounds each, 8 cpus,
load 1–3). A difference inside the band is noise; outside it is a change worth a look.

| Number | Band (two runs, load < 3) | From |
|---|---|---|
| Sub app birth, ms | ±0.02 ms (0.14–0.16) | 6 Release rounds |
| Sub app birth, KB | ±5 KB (122–128) | 6 rounds |
| Root birth, ms | ±1.5 ms (18.4–20.5) | 9 Release rounds |
| Root birth, KB | ±25 KB (2,357–2,377 at stage 5) | 3 rounds |
| Sub request app, ms each | ±0.15 ms (1.99–2.30) | 6 Release rounds |
| Sub request app, KB each | ±10 KB (603–614) | 3 rounds |
| Root request app, ms each | ±1 ms (17.2–18.3) | 6 Release rounds |
| Root request app, KB each | ±120 KB (3,636–3,858 at stage 5; 4,247–4,249 at stage 3b) | 6 rounds |
| Held MB, 0 / 1k / 2k / 3k | ±0.1 MB each; **flat means the last three within 0.2 MB** | 9 runs |
| Held MB after the rest | ±0.2 MB (root 1.7–1.8, sub 2.6) | 2 runs |
| `plang --test`, s | ±0.5 s (4.7–5.6); the coder saw 5.0–5.4 at load ~12 | 3 rounds |
| C# sweep, s | **±7 s** (40.6, 41.6, 54.7: 14 s apart at one load, in Modules and Runtime) | 3 rounds |
| C# build, clean, s | **±10 s when the machine is quiet** (33.8, 33.8, 47.5, 48.7, 53.5, 53.8 over 8 runs on 2 tips); two runs took 93 and 168 s with another session's compiler running beside them. A clean build is the number most hurt by a neighbour: a clean build above 60 s with `VBCSCompiler` busy elsewhere (`ps`) is the neighbour, run it again. | 8 runs |
| C# build, incremental, s | ±2 s (9.5, 10.1, 10.6, 11.6, 12.6, 13.8; 14.9 and 22.7 once each, with a neighbour) | 8 runs |

The sweep's red suites and `plang --test`'s counts are not noisy: they are a name, and any new name is a regression
(at stage 5 the sweep's 6 red are `Wire`'s, the same names as at stage 0; `plang --test` fails
`/app/type/item/BareLiteralJudgedByForm.test.goal`, 3 stale, 1 skipped).

A sub app is one made through the door, `new app.@this(path, actor)`, as a server makes a request's; a root is one as the
command line makes it, `new app.@this(path)`. Times are the median of 100 births (birth + dispose), bytes allocated
per birth + dispose; a request app is born, a goal loaded and run, disposed, × 3,000, Release.

## Rows

| Date | Branch | Commit | Load | Sub birth | Root birth | Sub request | Root request | Held, sub (MB) | Held, root (MB) | `plang --test` | C# sweep | Build, clean | Build, incr. |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
<!-- rows -->
| 2026-10-27 | snapshot-7c-wip | 23d61537b (snapshot-recheck) | 2.70 | 0.16 ms, 107 KB | 19.00 ms, 2312 KB | 2.03 ms, 529 KB | 20.25 ms, 3785 KB | 1.6 → 2.0 → 2.0 → 2.0 | 0.1 → 1.9 → 1.9 → 1.9 | 4.8 s (298 pass, 2 fail) | 73.0 s (red: Wire,Runtime) | - | - |
| 2026-10-27 | snapshot-7c-wip | d119e797f (snapshot-7c-wip-calm) | 2.88 | 0.16 ms, 110 KB | 22.52 ms, 2493 KB | 2.11 ms, 578 KB | 22.65 ms, 4076 KB | 1.7 → 2.1 → 2.1 → 2.1 | 0.1 → 2.0 → 2.0 → 2.0 | 6.9 s (298 pass, 3 fail) | 51.5 s (red: Wire,Data,Runtime) | - | - |
| 2026-10-27 | snapshot-7c-wip | d119e797f (snapshot-7c-wip) | 9.46 | 0.16 ms, 110 KB | 28.16 ms, 2495 KB | 2.65 ms, 583 KB | 22.31 ms, 4083 KB | 1.7 → 2.1 → 2.1 → 2.1 → 1.9 | 0.1 → 2.0 → 2.0 → 2.0 → 1.1 | 4.8 s (298 pass, 2 fail) | 110.1 s (red: Wire,Data,Runtime) | - | - |
| 2026-10-26 | windows | 149256d61 (windows-tip) | 2.91 | 0.14 ms, 109 KB | 18.93 ms, 2392 KB | 1.84 ms, 543 KB | 17.92 ms, 3839 KB | 1.6 → 2.1 → 2.1 → 2.1 | 0.1 → 1.9 → 1.9 → 1.9 | 4.5 s (298 pass, 1 fail) | 41.8 s (red: Wire) | - | - |
| 2026-10-25 | decider-key | 8786cc08d (decider-key-tip2) | 2.97 | - | - | - | - | - | - | - | 40.6 s (red: Wire) | - | - |
| 2026-10-25 | decider-key | 8786cc08d (decider-key-tip) | 2.85 | - | - | - | - | - | - | 4.5 s (297 pass, 1 fail) | 68.9 s (red: Wire) | - | - |
| 2026-10-25 | json-lines | 9ed572965 (json-lines-tip) | 2.85 | - | - | 1.79 ms, 538 KB | 17.51 ms, 3865 KB | 1.6 → 2.1 → 2.1 → 2.1 | 0.1 → 2.0 → 1.9 → 1.9 | 4.5 s (297 pass, 1 fail) | 41.7 s (red: Wire) | - | - |
| 2026-10-25 | bugs | ad0caedd7 (bugs-tip) | 2.88 | 0.14 ms, 109 KB | 18.45 ms, 2406 KB | 1.85 ms, 543 KB | 17.61 ms, 3857 KB | 1.6 → 2.1 → 2.1 → 2.1 | 0.1 → 2.0 → 1.9 → 1.9 | 4.5 s (294 pass, 1 fail) | 45.2 s (red: Wire) | - | - |
| 2026-10-25 | packages | 1510726b5 (packages-13) | 2.25 | - | - | - | - | - | - | 4.4 s (292 pass, 1 fail) | 84.7 s (red: Wire) | - | - |
| 2026-10-25 | packages | 1435afbf8 (packages-tip) | 2.73 | 0.14 ms, 109 KB | 17.07 ms, 2358 KB | 1.85 ms, 539 KB | 17.02 ms, 3821 KB | 1.6 → 2.1 → 2.1 → 2.1 → 1.9 | 0.1 → 1.9 → 1.9 → 1.9 → 1.1 | 5.4 s (298 pass, 1 fail; run by hand after the bench, the one run with the OpenAI key) | 39.7 s (red: Wire; the bench's own sweep ran with no key, so Modules' 80 mocked-LLM tests were red with MissingLlmKey: rerun with a dummy key, Wire's six only) | 30.9 s | 8.3 s |
| 2026-10-25 | fast-apps | 2401279ed (review7-after-restore) | 2.81 | - | - | - | - | - | - | 5.3 s (298 pass, 1 fail) | 40.5 s (red: Wire) | 75.7 s | 8.6 s |
| 2026-10-25 | fast-apps | 2401279ed (review7-tip: birth, request and held are valid; `plang --test`, sweep and build ran on a stale restore, 9.0.2 in the test projects' assets, and are void: see the next row) | 2.49 | 0.14 ms, 110 KB | 17.90 ms, 2358 KB | 1.82 ms, 540 KB | 16.68 ms, 3821 KB | 1.6 → 2.1 → 2.1 → 2.1 → 1.9 | 0.1 → 1.9 → 1.9 → 1.9 → 1.1 | 5.2 s (0 pass, 0 fail) | 34.9 s (red: Modules,Types,Wire,Data,Generator,Runtime) | 162.5 s | 9.2 s |
| 2026-10-25 | fast-apps | fb4e80b9d / d050b9f50 (builds only, 5 runs) | 2.9 at start | - | - | - | - | - | - | - | - | 33.8–53.8 s (93.4 once beside a neighbour's build) | 9.5–14.9 s (22.7 once) |
| 2026-10-25 | fast-apps | d050b9f50 (review5-d050b9f50) | 12.71 | 0.27 ms, 110 KB | 29.83 ms, 2360 KB | - | - | - | - | - | - | 98.3 s | 15.6 s |
| 2026-10-25 | fast-apps | dec53e6c4 (review4 N1) | 2.59 | 0.15 ms, 110 KB | 18.62 ms, 2359 KB | - | - | - | - | - | - | - | - |
| 2026-10-25 | fast-apps | d1fb4dd29 (review4-build-rerun) | 13.66 | - | - | - | - | - | - | - | - | 74.9 s | 9.1 s |
| 2026-10-25 | fast-apps | d1fb4dd29 (review4-02aff152e) | 16.25 | 0.14 ms, 110 KB | 18.48 ms, 2488 KB | 1.85 ms, 541 KB | 16.56 ms, 3720 KB | 1.6 → 2.1 → 2.1 → 2.1 → 1.9 | 0.1 → 2.0 → 1.9 → 1.9 → 1.1 | 5.6 s (298 pass, 1 fail) | 37.3 s (red: Wire) | 90.5 s | 79.4 s |
| 2026-10-25 | fast-apps | 26c9e0b9c (stage6-after) | 15.83 | - | - | - | - | - | - | - | - | 87.9 s | 11.8 s |
| 2026-10-25 | fast-apps | 37501ee53 (stage6-packages) | 20.87 | - | - | - | - | - | - | - | - | 65.4 s | 10.6 s |
| 2026-10-25 | fast-apps | f4f754455 (stage6-before) | 9.89 | - | - | - | - | - | - | - | - | 45.9 s | 11.5 s |
| 2026-10-25 | fast-apps | cc78ec913 (stage5) | 2.9 | 0.15 ms, 128 KB | 18.4–20.2 ms, 2.36 MB | 2.0–2.3 ms, 0.60 MB | 17.2–18.3 ms, 3.6–3.9 MB | 2.4 → 3.0 → 3.0 → 3.0 → 2.6 | 0.9 → 2.8 → 2.7 → 2.7 → 1.7 | 4.7–5.6 s (298 pass, 1 fail, 3 stale) | 40.6–54.7 s (red: Wire) | - | - |
| 2026-10-24 | fast-apps | 013396728 (stage 0) | 1.3 | - (no door) | 27–32 ms, 5.2 MB | - (no door) | 22 ms, 6.6–6.8 MB | - | 0.9 → 45 → 87 → 130 (no rest) | 17–27 s (294 pass, 1 fail, 3 stale) | 103–136 s (red: Wire) | - | - |

**Held memory is read inside the tool's own process**, which holds less than the test host the earlier bench ran in: the
tool's absolute MB are about 0.5–0.9 MB under stage 5's (the shape, flat or not, is the same). Compare a tool row with
tool rows; the first tool row below the stage rows is the calibration.

Stage 0 and stage 5 are from `.bot/fast-apps/tester/baseline.md` (three rounds each, a range where the rounds differ;
built by the earlier bench in `.bot/fast-apps/tester/bench/`). Their build columns were not measured. Rows from
`Tools/Bench/run.sh` give one number per column, not a range.
| 2026-10-27 |  | d72538402 (base-d72538402) | 2.47 | 0.16 ms, 109 KB | 23.88 ms, 2387 KB | 2.15 ms, 542 KB | 21.81 ms, 3841 KB | 1.6 → 2.1 → 2.1 → 2.1 | 0.1 → 1.9 → 1.9 → 1.9 | - | - | - | - |
