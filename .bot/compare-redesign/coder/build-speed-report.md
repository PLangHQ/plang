# Build & Test Speed Report

For Ingi, 2026-06-10. Everything measured on this container (8 cores, fast
overlay disk, .NET 10, warm where stated). The deliverable is `./dev.sh` at
the repo root — the numbers below are what it buys.

## The headline numbers

| Scenario | Before (what I was doing) | Now (`./dev.sh`) |
|---|---|---|
| `dotnet run --project PLang.Tests` per call | **90s+** | never used again |
| No-change build | 12–95s (inconsistent) | **1.1s** |
| Edit inside PLang (method body) → build | ~13s | **4.6s** |
| Edit a test file → build | **47s** | **31s** (see "the last bottleneck") |
| Edit PLang public API → build | ~48s | ~31s (test project must recompile) |
| Run one test class | 90s+ (via dotnet run) | **~3s** (direct binary + filter) |
| Full C# suite (4,270 tests) | — | ~37s run time |
| PlangConsole build | — | **1.2s** |

Typical inner-loop iteration (edit PLang + run affected test class):
**was ~5 minutes, is now ~8 seconds.**

## Where the time actually went (root causes)

1. **`dotnet run` was the main villain** — every call pays restore + MSBuild
   evaluation + build + run. The fix is plain `dotnet build --no-restore`
   once, then executing `PLang.Tests/bin/Debug/net10.0/PLang.Tests` directly.

2. **Analyzers: 17s of every test-project compile.** Measured with
   `-p:ReportAnalyzer=true`: TUnit.Analyzers 5.0s (4.0s of it ONE analyzer —
   DisposableFieldProperty), .NET NetAnalyzers 5.1s (2.8s of it CA2252
   "preview features"), plus the rest. `-p:RunAnalyzers=false` removes them
   from dev builds. Source generators are NOT analyzers — TUnit's test-metadata
   generator and PLang.Generators still run, so tests stay correct.
   **Critical catch:** the flag must be used consistently — flipping it
   between builds invalidates MSBuild's incremental state and forces a full
   rebuild (measured: a "fast" build right after a flag flip cost 58s).
   `dev.sh` hard-codes it so it can't flip by accident.

3. **PLNG001/PLNG002 and TUnit warnings still gate commits.** Analyzers-off is
   for the inner loop only. `./dev.sh full` builds with analyzers ON and runs
   both suites — that is the pre-commit/pre-handoff command, so nothing lands
   without the System.IO ban and property-shape gates having run.

4. **Reference assemblies already protect us** (verified): a body-only change
   in PLang leaves `ref/PLangLibrary.dll` untouched, so PLang.Tests skips its
   recompile entirely. That's why a PLang-internal edit is 4.6s. Only public
   API changes (or test edits) pay the test-project compile.

5. **The after-idle stall (2 minutes, zero CPU).** Reproducibly, the FIRST
   build after a long idle period can hang ~2min using no CPU, then everything
   is fast again. Network is fast (probed), it's not restore, not concurrent
   test runs (tested both). Best theory: WSL2 memory/page-cache reclaim — the
   first big process tree after idle stalls while the VM rehydrates. Not fully
   root-caused. Mitigation that works: `./dev.sh warm` run in the background
   at session start absorbs the stall before it costs anyone anything.

## The last bottleneck: 31s to recompile the test project

PLang.Tests is one project with 4,270 tests. Any test edit (or PLang API
change) recompiles all of it: ~25s of csc + 4s of TUnit metadata generation,
even with analyzers off. The only real fix is structural: **split PLang.Tests
into per-area projects** (App.Core, Serialization, Modules, Generator, …).
Then a test edit recompiles one slice (estimate: 3–8s) and slices build in
parallel. It's a bigger change (project files, GlobalUsings, InternalsVisibleTo
per project) — worth it if the 31s bothers you in practice, but I didn't want
to restructure the test tree without asking. Say the word.

## What you / the bots should do (the whole recipe)

```bash
./dev.sh warm   &      # once, at session start (background — absorbs the idle stall)
./dev.sh build         # after edits: incremental, analyzers off
./dev.sh test Foo      # run test classes matching *Foo* (~3s once built)
./dev.sh test          # full C# suite
./dev.sh ptest         # plang tests (builds console, runs from Tests/)
./dev.sh full          # BEFORE COMMIT: analyzers on + both suites green
```

Never again: `dotnet run --project PLang.Tests`, `dotnet test`, ad-hoc flag
combinations (incremental-state thrash), foreground full sweeps while
iterating (use a filter; the full sweep is for checkpoints).

## Open items

- The after-idle stall is mitigated, not root-caused. If it ever bites
  through the warmup, capture it with `dotnet build -bl` and read the binlog.
- Splitting PLang.Tests (31s → ~5s for test edits) — awaiting your call.
- A recurring segfault at the END of full test runs (after results print —
  results are valid). Pre-existing, intermittent, unrelated to speed; on the
  watch list.
