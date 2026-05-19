# plang vs Node.js benchmarks

Apples-to-apples runtime benchmarks. Runtime2 has no webserver yet (the v1
webserver was removed in `cb63ee564`), so we compare equivalent **compute
workloads** executed by the `plang` binary vs a `node` script: same N
iterations, same work per iteration, same measurement window.

What we measure on the *host process* (the spawned `plang` / `node`):

- Wall-clock time (s)
- CPU time, user + system (s) — `resource.getrusage(RUSAGE_CHILDREN)`
- Max RSS (MiB) — same
- Throughput (ops/sec) — N / wall

What we measure *inside* each run (one sample per iteration, emitted as a
`DURATIONS_NS=` line that the harness parses):

- p50 / p95 / p99 latency per operation (ns)

On the plang side, per-iteration durations come from the callstack:
`Flags.Timing` is on under `--debug`, so each Call records `StartedAt` /
`CompletedAt`, and the goal walks `%!callStack.Root.Children%` after the
loop to emit one duration per child.

On the Node side, per-iteration durations come from
`process.hrtime.bigint()` deltas. The instrumentation overhead is paid on
both sides — that's the honest comparison.

## Workloads

| Workload | What it does                              | Status                               |
|----------|-------------------------------------------|--------------------------------------|
| hello    | Trivial noop — assign a string literal     | plang ✓ node ✓                       |
| json     | Render a small object to a JSON string     | plang (via template render) ✓ node ✓ |
| file     | Read a small file from disk into a var     | plang ✓ node ✓                       |
| sqlite   | `SELECT` one row from an in-mem SQLite DB  | **plang ✗** (no module) — node ✓     |

The `sqlite` plang side is intentionally left out — Runtime2 does not
expose a SQLite action yet (the Sqlite NuGet ref in `PLang.csproj` is
unused). Adding one is a separate piece of work.

## Layout

    benchmarks/
      harness/
        run.py           # orchestrator — runs every (workload, runtime) pair
        measure.py       # single-shot measurement (wall/CPU/RSS + per-iter parse)
      workloads/
        hello/{bench.goal,bench.js}
        json/{bench.goal,bench.js}
        file/{bench.goal,bench.js,fixture.txt}
        sqlite/{bench.js,seed.sql}   # node only
      results/
        <YYYY-MM-DD>/{raw/,summary.md}

## Running

Rebuild plang first (the stale-binary trap in the project CLAUDE.md applies):

    rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
           PLang.Generators/bin PLang.Generators/obj
    dotnet build PlangConsole

Then from the repo root:

    python3 benchmarks/harness/run.py            # all workloads, both runtimes
    python3 benchmarks/harness/run.py --workload hello
    python3 benchmarks/harness/run.py --n 1000 --repeats 5

Defaults: N=10000 iters per run, 3 warmup runs, 5 measured runs, results
written to `benchmarks/results/<today>/`.

## Caveats

- **Startup cost dominates small N.** plang spins up a .NET runtime + scans
  modules; `node` starts a V8 isolate. Both are real costs but neither is
  what you usually care about when comparing "doing 10k operations." We
  report wall *including* startup AND ops/sec computed *only over the
  measured loop window* (timer-bracketed). Look at both.

- **plang's per-step overhead is non-trivial.** Each iteration in a
  `foreach` allocates a Call frame, runs the source-generated action, and
  records timing. A JS `for` loop has none of that. That gap is the
  point of the benchmark — it shows the cost of plang's per-step
  bookkeeping, which is the cost of every feature plang gives you
  (callstack, diffs, recovery, tracing).

- **One-machine numbers, not microbenchmarks.** Noise floor on a shared
  WSL host is wide. Re-run a few times before drawing conclusions.
