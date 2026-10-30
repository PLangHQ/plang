# app.diagnostic — a measuring tool inside plang (proposal, not built)

Ask (Ingi): `app.diagnostic`, off by default; enabled, it collects data per goal, step and action, with memory, cpu and
csharp sections, readable by a plang developer and good enough for our own speed work (90%+ of what Tools/Bench does).

**Pattern first.** 3 a type per kind, decided at birth (`diagnostic.off` / `diagnostic.on`, no `if (enabled)` in the
runtime); 11 lazy (nothing is made until a span is asked for); 7 one door (every span is `diagnostic.Span(...)`);
dot paths (`app.diagnostic.memory.heap`); data maps by view and tags (`[Out]`, `[Store]`), no envelope type.

## What I learned measuring by hand (the tool has to handle these, or its numbers lie)

| Trap | What happened | What the tool does |
|---|---|---|
| JIT tiering | a goal took 600 us at run 100 and 117 us at run 30,000 | every row keeps a per-run-index series, so "cold", "settling" and "settled" are separate numbers |
| Sampling profiler bias | dotnet-trace blamed `Guid.NewGuid` (20%) and locks (37%); both are < 1 us | timed spans are the primary tool; a sampler is only a hint and is labelled so |
| Thread-local allocation counter | negative or wrong bytes across an `await` that hops threads | `memory.bytes` says which clock it used (thread, or all threads when runs are sequential) |
| Loaded machine | cold numbers inflated 2x | every report carries the machine load and the CPU time next to wall time |
| Observer effect | the listener adds its own cost | the tool measures an empty span at start and subtracts it, and reports the overhead |
| Held memory | growth with request count is the leak signal | `memory.held` forces a GC after N runs and reports what stays |

## The shape

```
app.diagnostic                 off | on (a type per kind, chosen at app birth from setting diagnostic)
  .setting                     what to collect: time (default) | memory | full ; csharp names to include
  .time                        wall, self, inclusive, count; per address
  .cpu                         cpu time (Environment.CpuUsage), load average, threads in use
  .memory
    .bytes                     allocated per span (inclusive and self)
    .heap / .workingset        sampled by the runtime counters
    .gen0 .gen1 .gen2          collections during the span, and the pause
    .held                      bytes still held after a forced GC (the leak check)
    .type                      allocation ticks by type (what Bench ticks does today)
  .jit                         methods compiled and JIT ms during the span (JitInfo, per thread)
  .lock                        contention count during the span
  .exception                   first-chance exceptions during the span
  .csharp["shell.Resolve"]     a named span placed in C#; same data as the above, for a hot path inside an action
  .run                         one row per run: index, wall, bytes (the tiering series)
```

An **address** is what plang already has: `goal/Start`, `goal/Start/3`, `goal/Start/3/variable.set`
(`action.Address`). A row is `count, total, self, min, max, p50, p95` (a fixed histogram, no raw sample list).
Self = inclusive minus the children's, from the call frame's own parent chain.

## How it hangs on the runtime (the part that must cost nothing when off)

The call frame already exists for every goal, step and action (`call.Push` / dispose in `action.Start`). That is the
one place a span is born:

```csharp
// in the call frame's birth and dispose — the only runtime change
using var span = app.diagnostic.Span(this);   // off: returns default, 1 virtual call, no allocation
```

- `diagnostic.off.Span` returns `default` (a struct): nothing allocated, nothing read.
- `diagnostic.on.Span` takes `Stopwatch.GetTimestamp()` and the allocation counter at birth; at dispose it adds the
  difference to the address's row and subtracts it from its parent's self. About 60-100 ns a frame, against 12 us a step.
- It is not an event subscriber. Events are async awaits and would be part of what is measured.
- The csharp spans are the same door: `using var _ = app.diagnostic.csharp["shell.Resolve"].Span();` placed where a
  C# path needs a clock; `off` makes it a no-op, so a span can stay in the code.

## Use from plang

```
/ a run with diagnostics, then ask it
Start
- start diagnostic, memory
- run goal Checkout 2000 times
- stop diagnostic
- write out %!app.diagnostic.time.slowest 5%
- assert %!app.diagnostic.memory.bytes["goal/Checkout"].self% < 70 KB, "a checkout run stays under 70 KB"
```

CLI: `plang --diagnostic` (time), `--diagnostic.setting.level=full`, `--diagnostic.setting.report=json`.
A byte or time pin in a plang test then guards its claim from inside plang (the repo rule on byte pins).

```
goal/Checkout            runs 2000   wall 118.2 us (self 12.1)   alloc 61 KB   jit 0 ms   gc0 29
  step 1  set            wall  19.5 us (self  3.0)                alloc  4 KB
    action variable.set  wall  16.5 us (self  8.0)                alloc  4 KB
      csharp shell.Resolve   1.5 us
  step 2  if / call      wall  31.4 us ...
```
(the numbers above are my measurements of the Condition test, shown only to show the layout)

## Output

- A table at the end of the run (above), and `.diagnostic/<run>.json` from the tagged members (`[Out]`/`[Store]`); no
  parallel wire type.
- Export to Chrome trace / speedscope for a flame view (phase 4).
- `compare a b`: two runs side by side, flags any row outside the noise band (what record.md does by hand).

## Phases

1. **Time + bytes per goal/step/action** through the call frame, CLI flag, table report, JSON. Overhead calibration.
2. **csharp spans, JIT, GC, lock and exception counters**, the run series (tiering).
3. **Read from plang** (`%!app.diagnostic...%`), assertions, `compare`, pins in plang tests.
4. **Allocation by type, runtime counters, export**; an optional sampler that shells out to dotnet-trace, labelled biased.

Bench (`Tools/Bench`) stays as the outer harness: a fresh process, load gating, cold start, the 5-minute rests and
`record.md`. Its modes (birth, request, repeat, steps, phases, jit, alloc) become views of `app.diagnostic` rather than
their own meters.

## Locked core — needs Ingi's yes before anything is built

- A new `app` member (`app.diagnostic`) is an addition to `app`'s surface.
- The call frame gets a span (private to call memory, no public member, `Id` and the rest stay).
- Alternative that avoids both: a module (`module/diagnostic/`, actions `start diagnostic`, `stop diagnostic`,
  `report diagnostic`) owning its types, with the call frame's span as the only core touch. "New needs go to the edge."

## Not covered (the 10%)

- Cold start's JIT cost inside the runtime before plang runs (needs `Bench cold`/`jit`, a fresh process).
- Anything below a span: a statement-level CPU profile still needs a profiler, with its safepoint bias.
- Memory of the process outside plang's own allocations (native, runtime caches): `workingset` shows it, not why.
