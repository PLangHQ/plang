# The cost of calling: what one action, one call and one `%!goal%` read cost, in pieces

Asked by the architect for Ingi ("the cost of going through goal.call … just general when calling an action"). Read and measure only; nothing committed but this file.
Measured on event-action 3266637da (after decision 64's set fix), sub app (`Bench cost child`), `DOTNET_TieredPGO=0`, 10,000 runs after 2,000 warm-up, a loaded machine (load 10–15 on 8 cpus).
- **Bytes are exact** (this thread's allocations per run, repeated to the byte between runs).
- **Times are cpu microseconds**, the second of two runs; they move ±30–40% with the machine's load (the same piece read 175 and 270 µs in the two runs). Read the shares of bytes first, then the times. A piece's time is not additive with the others (async state machines and GC sit between pieces).
- Pieces are measured alone where they can be called alone, otherwise as the difference of two layers; each table says which. The scratch harness (modes `cost`, `reads`, `costtypes`) is a copy of `Tools/Bench/Program.cs` with the three modes added, kept in the scratchpad; it is not committed.

## What the numbers say, first

1. **The event lists are not the cost.** `On(step)`, `On(catalog action)` and the empty before/after cost 0.05–0.26 µs and 0–184 bytes per action: under 1.5% of an action. The per-actor lists of event-action are cheap when nothing is bound.
2. **A `%!x%` read whose name memory does not hold costs ~41 KB and ~180 µs, and 38 KB of it is one avoidable thing:** `app.shortcut.Get(name)` walks the six system shortcuts and each `Match(key)` recomputes `Name => Path.Stem.ToString()`, which allocates **5,568 bytes and takes ~37 µs each time** (a path stem made, a text made, a type minted with reflection). Not a goal call at all: `%!call.Current.Depth%` in a running frame is 43.4 KB, `%!data%` (bound) 0 bytes, `%!app.goal%` 680 bytes.
3. **The goal-call cost Ingi sees is mostly (1) frames, (2) minting a `type` per ask, (3) the dispatch building two handler objects and binding every parameter through closures, and (4) the `%!x%` miss above.** Each is below.
4. `shortcut/this.code.md` says a read costs ~0.2 ms against ~0.03 ms for a value in memory. Measured: 0.2 ms (206 µs wall, 250 µs cpu, 46,071 bytes) against **0.2 µs, 0 bytes**: the value in memory is a thousand times cheaper, not seven.

## Case A: the focus case, `%!goal%` (46,071 B; 206 µs wall, 250 µs cpu)

| Piece | Time (cpu) | Bytes | % of bytes |
|---|---|---|---|
| memory lookup of `!goal` (a miss) | ~0 | 0 | 0% |
| `app.shortcut.Get("goal")`: walk, `Match` ×2 (4th of 6; each `Match` = `Path.Stem` 5,568 B) | 74 µs | 11,496 | 25% |
| `call.Isolate` + dispose | 12 µs | 3,336 | 7% |
| the shortcut goal's `goal.Start` (inside `Read`, after Isolate) | ~120 µs | ~30,960 | 67% |
| · its step layer (Setting, frame, `On(step)`, before/after, chain loop) | | 5,217 | 11% |
| · its action frame (the `return`'s) | | 3,368 | 7% |
| · the `return` action's dispatch (Instance, Resolve, `%!app.call.scope.caller.goal%` read 1,896 B, the return's Data) | | ~22,200 | 48% |
| · the goal's own layer (frame, `Broken()`, `On(goal)`, `Leave`) | | remainder, ~0.2–8 KB | |
| `%x%` read for comparison | 0.2 µs | 0 | |

Notes: alone, `goal.Start` of the shortcut goal measures 38,763 B and inside `Read` ~31,000 B (the frames are cheaper under an Isolate); the 8 KB difference is the one place the layers do not add up. The `return` action (25,587 B) costs 10 KB more than the `set` action (15,857 B): its dispatch is the largest single piece.

## Case B: a step running one action, `set %x% = "hello"` (step 21,074 B; 51 µs wall, 59 µs cpu)

| Piece | Time (cpu) | Bytes | % of bytes |
|---|---|---|---|
| step layer (Setting, step frame, `On(step)`, before/after, chain loop, `Settle`) = step.Start − action.Start | ~24 µs | 5,217 | 25% |
| action frame (push + dispose) | 4.2 µs | 3,368 | 16% |
| events: `On(catalog)` + the actor's before/after + the clauses (all empty) | 0.3 µs | ~184 | 0.9% |
| dispatch: `Instance()` (`Module[Name]`, the unknown-property check 304 B, `Module.Create` the throwaway shell 1,592 B) | 3.7 µs | 1,896 | 9% |
| dispatch: `shell.Resolve` (the real handler, every parameter bound lazily) | 14.9 µs | 6,528 | 31% |
| dispatch: `real.Start()` (`Set.Start`, its `Variable.Set` 2,024 B) | 4.1 µs | 1,992 | 9.5% |
| `Variable.Set("!data", …)` after the action | 1.5 µs | 344 | 1.6% |
| not broken out (async machinery, try/catch frame, the `Stale` check) | ~6 µs | ~1,545 | 7% |
| action.Start total | 34.8 µs | 15,857 | 75% of the step |

By type (allocation ticks, bytes per action of 15.9 KB): `data` 1.9, strings 1.2, closure classes 1.2 (+0.2 + 0.2 + …), **minted `type` + `kind.empty` + `data<type>` 2.6**, a `json` serializer made per Resolve 0.66, `Func<property,bool>` 0.6 (the unknown-property check), the frame's `VolatileNode[]` + `Dictionary` + `Tables` (its variable store) ~1.2 per frame.

## Case C: `call Inner`, a goal of one step (`set %innerDepth% = %!call.Current.Depth%`) (96,450 B; 339 µs)

| Piece | Time (cpu) | Bytes | % of bytes |
|---|---|---|---|
| the calling step's layer | ~24 µs | 5,217 | 5.4% |
| `goal.call` action frame | 4 µs | 3,368 | 3.5% |
| `goal.call` dispatch (Instance, Resolve of Name/Parameter, the call, `goal.list.Find` 2,536 B; difference of layers) | ~50 µs | ~15,000 | 15.6% |
| inner goal's own layer (frame, `Broken()` LINQ, `On(goal)`, `Leave`) = goal.Start − step.Start | ~15 µs | 8,297 | 8.6% |
| inner step layer | ~24 µs | 5,217 | 5.4% |
| inner action frame | 4 µs | 3,368 | 3.5% |
| inner `set` dispatch (the same as case B's, 12 KB) | ~25 µs | ~13,091 | 13.6% |
| **the variable read `%!call.Current.Depth%`** (of which `shortcut.Get` miss 40,568) | 174 µs | 42,680 | **44.3%** |

Without the read, `call Inner` is ~54 KB and ~165 µs: five frames (goal.call step, its action, inner goal, inner step, inner action) are 17 KB of it, the two dispatches ~28 KB.

## Ranked ideas

Each names its OBP pattern, the number it saves against the pieces above, and whether it touches a locked surface. None adds a second path: each shrinks work the single door already does.

1. **A shortcut's name is a birth fact (pattern 5).** `Name` is kept at construction (it is the file's stem and never changes), so `Match(key)` is a string compare. Saves 5,568 B and ~37 µs per `Match`: a `!x` read that misses memory goes 41 KB → ~1 KB (−97%), `%!goal%` 46 → ~35 KB (−24%), `call Inner` 96 → ~56 KB (−41%). Not locked (a private member of `shortcut`); two lines. **The largest single win, and it speeds every `!name` read that is not bound in memory (`%!call…%`, `%!llm%`, `%!channel…%`).** Companion (pattern 7): the shortcut list answers by name through its own index, not by walking six items; saves ~0.5 KB, not needed once Name is kept.
2. **A type is found, not made (pattern 5/7).** Asking an item for its `Type` (`Type => new(typeof(@this))`) or building a `data<type>` mints a `type` and a `kind.empty`, and reading attributes by reflection (`FormatAttribute`, `CustomAttributeRecord`, `RuntimeMethodInfoStub`, `Func<OwnedClr,bool>`) on the first build of a kind. Per action ~2.6–3.4 KB of 15.9 (−17–21%); in the shortcut miss, 34 KB of 40 are this (after idea 1 it is gone from that path). Hot sites to find: every `.Type` of a leaf item, `Data(name, value, type)`, the kind of a text. A type already has an identity (`{Name, Kind, Strict}`), so one made once per class and kind, held by the type list, is the same answer. The type system is locked: this is a private refactor (no member added) but changes the identity of what `.Type` returns (two asks → one instance); the coder checks `ReferenceEquals` uses. Number: B −2.8 KB (−13% of the step), C −8 KB (−8%).
3. **A frame's variable store is made on its first write (pattern 11).** Every frame (goal, step, action, isolate) builds its own store (`ConcurrentDictionary` buckets `VolatileNode[]` 0.64 KB, `Dictionary` 0.37, `Tables` 0.14 per tick-sample; ~1.2–1.7 KB) though almost none writes a local. A case-B step has two frames (−2.6 KB, −12%), `call Inner` five (−6.5 KB, −7%), `%!goal%` four with the Isolate (−5 KB, −11%). The call memory is locked; this changes how a frame holds its store, with no member added.
4. **The handler is made once per run, not twice (pattern 12, one birth method; generator).** `Instance()` makes a throwaway shell (`Module.Create`, 1,592 B) only to call its generated `Resolve`; the real handler is `Resolve`'s output. The generator emits a static `Resolve(action, context)` on each action class and the registry calls it, the shell goes. Also the unknown-property check (`Property.FirstOrDefault(p => element.Property[p.Name] == null)`, 304 B per run) is a fact of the loaded action against the catalog, judged once like `Stale` already is. Saves ~1.9 KB per action (−9% of case B's step, −12% of an action), 3.7 → ~1 µs. Touches the generator's emitted shape and the action's door: the architect's lock; needs Ingi.
5. **Parameters bound by emitted code, not closures (pattern 12; generator).** `Resolve` costs 6,528 B and 14.9 µs: `GetParameter(name).As<T>(Context)` per property, closure classes, a `json` serializer made per Resolve (0.66 KB), `data<bool>`/`data<type>` wrappers. The generator can emit the typed assignment of each lazy property directly. Saves an estimated half of Resolve, ~3 KB and ~7 µs per action (−19% of an action); an estimate from the tick types, not measured. Touches the generator's emitted shape (locked).
6. **A goal is found by an index, not a rebuilt list (pattern 5).** `goal.list.Find` is 2,536 B and 3–5 µs a call (`Items().Where().Reverse().ToList()` up to twice; my finding 5 at 1268c81f7, still there). A name → goal map per folder, made when the folder's goals are listed. −2 KB per call (−2% of `call Inner`). Not locked.
7. **`Broken()` and the other per-start checks that give a fixed answer are judged once (pattern 11).** `goal.Start` runs `Step.Items().Where(s => s.Error != null).ToList()` at each start; a goal's steps do not change after load. ~0.2–0.5 KB per goal start; small, listed because it sits in the 8.3 KB goal layer.
8. **Compile at build and keep it in the .pr, or compile at run (`app.jit`); an idea, not a design.** What a compiled goal removes of the pieces above: `Instance` + shell (1.9 KB), `Resolve` (6.5 KB), the unknown-property and `Stale` checks, the closure/`data<type>` wrappers (~2 KB), i.e. **~10 of an action's 15.9 KB and ~19 of 29 cpu µs: −60% bytes, −65% time per action**. What it must keep: the frames (3 per goal-with-one-step: 10 KB; the call stack is observable and errors, `%!event%`, debug and resume read it), the event asks (cheap), and the variable writes. Net on case B's step: 21 → ~11 KB (−48%). **Cost, estimated and not measured here:** a first compile of a small goal through Roslyn in the process ~0.3–1.5 s cold and 30–80 MB for the compiler's own state (shared by all goals after), ~30–150 ms and ~1–5 MB per further goal, ~10–60 KB of loaded assembly per goal (collectible assemblies to unload). Break-even at ~15 µs saved per action: a goal run less than a few thousand times loses. It is the only idea here that removes the dispatch cost instead of trimming it; ideas 4 and 5 get 40% of the same saving with the generator at build time and no run-time compiler. It touches the action's door and the call memory (locked) and adds a second way to run a goal unless the interpreter *is* the compiled form for every goal: a fork if both stay.
9. **Not an idea: a fast path for `%!goal%` or for a goal of one step.** Answering `%!goal%` from the call stack without running `/system/shortcut/goal.goal` is a second path beside the door, and the system shortcut is overridable by the app's own copy (the point of it). Ideas 1, 2 and 3 bring the read from 46 KB to ~27 KB (−41%) through the same door; it stays ~100× a memory read, which is the price of a read that is a goal.

## Order by value for effort

| # | Idea | Pattern | Saves (case) | Locked? | Size |
|---|---|---|---|---|---|
| 1 | shortcut `Name` kept | 5 | −11 KB A, −40 KB C, −39 KB every other `!x` miss | no | 2 lines |
| 2 | a type found, not made | 5/7 | −2.8 KB per action | private refactor of a locked surface | medium: find the sites |
| 3 | frame store on first write | 11 | −1.2 KB per frame | call memory (private) | small |
| 6 | goal index | 5 | −2 KB per call | no | small |
| 4 | one handler per run, check judged once | 12/5 | −1.9 KB per action | generator shape, action door | needs Ingi |
| 5 | emitted parameter binding | 12 | ~−3 KB per action (estimate) | generator shape | needs Ingi |
| 7 | `Broken()` once | 11 | −0.3 KB per goal | no | trivial |
| 8 | compiled goals | — | −48% of a step (estimate) | action door, call memory | a design |

After 1+2+3 (arithmetic on the pieces above, not measured): case A 46 → ~27 KB (−41%), case B step 21 → ~16 KB (−25%), case C 96 → ~45 KB (−53%). The measurements are repeatable with the harness; I have not tried any of the changes.

---

# Addendum — what the open questions measured (Ingi asked; for the call-cost branch)

All with `Tools/Bench` (modes `phases`, `repeat`, `cold`, `loadtypes`; `DOTNET_TieredPGO=0`, load 3–6, bytes exact, times best-of or repeated). The one source change was a mutation (item 3), announced and reverted.

## A. Where a request app's 441 KB goes (the Condition.test.goal request)
born 99 KB + goal load 40 KB + goal run 300 KB + dispose 2 KB. The same goal run again in the same app costs **230 KB flat** (500 runs, no growth): the first run pays ~70 KB of one-time work (per-actor lists, type/kind minting). It is garbage per run, not held; GC pause is **5.8% of wall** (1.1% with a bigger gen0), so bytes matter as work not done, not as GC.

## B. Step cache made at birth — IDEA 10 (pattern 11), biggest win found
`app/this.cs:199` `Cache { get; internal set; } = new module.cache.Memory()` wraps a .NET `MemoryCache` (`module/cache/Memory.cs:11`): **47 KB and 29 µs per app**, made and disposed (`new Memory()` + Dispose measured alone). Only `on.cache` (`module/on/cache.cs:40,60`) and the signing nonce (`signing/code/Ed25519.cs:142`) ask for it. Mutation (made on first ask, `Dispose` skips an unmade one): **sub birth 101 → 55 KB and 0.48 → 0.14 ms (3.4×); root birth 2265 → 2206 KB; the request's run/load unchanged.** The 5.8 KB in the fast-apps notes (§24, bare request app) is of this order. Not run: the C# and plang suites with the change (the mutation was a measurement).

## C. The step layer — the "24 µs" I raised was a loaded-machine artifact
Best of 7 rounds: `step.Start` 29.1 µs / 20.5 KB, `step.Code.Start` 23.7 / 16.4 KB, `action.Start` 23.0 / 16.2 KB. The step layer is **~5 µs and ~4 KB** (its frame 1.4 µs / 2.5 KB; the rest the awaits and the two empty moments); the action list adds 0.7 µs. Nothing to fix there beyond the frame store (idea 3). Of an action's 23 µs, the real work is ~4 µs (as before).

## D. The goal load (40 KB), by type (ticks, born vs born+load)
String 9.5 KB, `FileInfo` 4.1 + `path.file` 3.2 (file stats), String[]/AppendPrepend 4.3 (probing candidate locations), type/kind objects ~5.6 (idea 2 again), the rest small. About 8 KB looks like file probing the load repeats (how many stats per load is not counted); ~6 KB is the type mint. Not all avoidable: the `.pr` text and the goal's objects are real. Sampling is coarse (±1 KB per type).

## E. Cold costs (fresh process, never seen before) — IDEA 11, the largest time cost
One fresh process, root app: **born 245 ms (JIT 95 ms), first goal load 168 ms (JIT 129), first run 47 ms (JIT 43) = ~460 ms, of which JIT ~270 ms (~60%), ~3,200 methods compiled**; the same again in the same process: born 31, load 5, run 3 ms. A child app after a cold server: born 5 ms, load 175 ms, run 48 ms. The real CLI (`plang Condition.test.goal`, Debug) takes **0.60–0.67 s wall**; nothing in the csproj sets ReadyToRun or tiering. A trial: `dotnet publish -r linux-x64 -p:PublishReadyToRun=true` (Debug config, `os/` copied beside it) runs the same goal in **0.35 s: −45% of the CLI's wall time** with the same output. This is time, not bytes, and it is a build/publish setting, not a code change (it needs the ReadyToRun compiler at build, and the file is bigger).

## Ranked additions to the list above
10. Step cache made on its first ask (B): −46 KB and −0.3 ms per app birth; four lines in `app/this.cs`. Small, no design question.
11. ReadyToRun for `PLang.dll`/`plang.dll` (E): −45% of a cold CLI run in a trial. Ingi's call (build and shipping).
12. Goal load: type mint (with idea 2) and the file probing (D): ~14 KB of 40.
Unchanged: ideas 1–3 stand; the step layer (C) is not a target. Not done: ideas 2+3 in a scratch copy (the 25–50% remains arithmetic), the number of stats per load, the suites with idea 10.

## F. Where a steady-state run's 230 KB goes, and idea 2 tried (second addendum)
`Bench repeat` with `BENCH_TICKS=1` (types allocated by the runs after the third): `kind.empty` 26 KB, `type` 20, `data` 16, the variable stores' `VolatileNode[string,data][]` 15 + `Tables`/`ConcurrentDictionary` 5, strings 15, closures ~16, `CustomAttributeRecord[]` 8, `Task<data>` 7: **type and kind making is ~30% of a run, the variable stores ~9%.**
A probe in the `type` constructor (sampled stack, 50 runs, reverted) named the sites: **~55 types are made per run, nearly all from `number`'s implicit operators** (`number.this.op_Implicit` ← `call.list.Push`, `call.this.get_Depth`, `list.get_Count`, `goal.step.list.Start`): `number.this`'s operator birth (`type/item/number/this.cs:36`) does `new type("number", typeof(number)) { kind = kind }`, and that constructor first makes a `kind.empty` the initializer throws away. The rest are two `bool` readers (`type/item/bool/serializer/Reader.cs`, 8 per run) and `type.serializer.Reader`.
**Trial (announced, reverted): the operator-born type kept per kind** (a `ConcurrentDictionary<kind, type>` in `number`, 3 lines): steady run **230 → 178 KB (−23%)**, first run 300 → 246 KB, per run 892 → 757 µs (−15%, a loaded machine); the number-tower suites (Storage, KindDerivation, Arithmetic, Read) green; the full suites not run. This is call-cost idea 2 for `number` alone; the same shape (a type per kind, made once) for the two `bool` readers is the next ~8 KB (3%).
Frame collections (idea 3's smaller half): `Tags` and `Errors` made on first use instead of in the frame constructor: 230 → 223 KB (−3%) — not worth it alone. The variable store (`ConcurrentDictionary` rows, ~20 KB, 9%) not tried.
Stats per goal load: not counted (no strace; `FileInfo`/`path.file` are 7 KB of the 40 KB load).

## G. What `shell.Resolve` is made of (Ingi asked; `Bench resolve`, best of 7 rounds, a `set %x% = "hello"` action)
`Instance()` 2.7 µs / 1.9 KB + `Resolve` 9.3 µs / 6.5 KB = ~12 µs / 8.4 KB of dispatch per action, before the action's own ~4 µs. Resolve's pieces: the real handler's constructor 1.2 µs / 1.2 KB (the shell's own, in `Instance`, is 1.7 µs / 1.6 KB, thrown away); **`context.Setting.Get(action, option)` for each parameter the step did not give — 1.5 µs / 1.0 KB each, two per `set` (type, default), the same "not set" answer every run** (it builds two address strings, walks the scope chain, awaits two `Saved` rows and makes a `NotFound` error); a typed view per given parameter 0.2 µs / 0.4 KB each; the rest (~4 µs / ~2.7 KB) is the generated Resolve's `__Default`, `Uninitialized`/default `data`s, `As<T>` and the state machine.
- **Idea 4 (one handler per run, generator): −2.2 µs / −1.9 KB** (the shell and the property check go).
- **New idea 12: an absent option judged once per action** (keyed by the setting's change count, which `Set` already bumps with `Move()`): **−3 µs / −2 KB** here, more for an action with more optional parameters. Not a generator change: the lookup is in `actor/setting/this.cs:109`, called from the generated `Resolve`.
Together ~−5 of the ~12 µs (−40% of dispatch, ~−20% of an action's ~23 µs). Not tried.

## H. Cold start of the root app, and a hello-world (`write out`) as a sub app (Ingi asked; no ReadyToRun — it was used only as a measuring idea, not run)
`BENCH_GOAL=/module/output/Output.test.goal` (`write out "test output message"`), `Bench cold`. Marks added to the constructors for the measurement only (announced, reverted).
- **A cold root app** (`new app.@this(path)`, unloaded machine ~210 ms, JIT ~70): the type list's scan is the largest piece, ~105 ms: `origin.code` makes `new type.list.@this(this)`, whose `Scan()` builds a `Code.Library` by reflection over the whole assembly (`assembly.GetTypes()` ~10 ms, `FamilyOf`/`NameOf` per class ~25, `new Mark(t)` per class ~18, the serializer-folder `Readers` ~10, then ~38 ms building entries from them). The constructor body after that is ~35 ms (`AppCode` 10–14, the action catalog 10–12, the rest small). A second root app in the same process takes ~30–60 ms in all, so most of the first one's cost is cold reflection, JIT, and type loading, not logic. Under load 15–20 the same step measured 260–490 ms: read these on a quiet machine.
- **`origin.code` is "a new one for each app"** (its summary): every root app scans the assembly again (the library is per `Code` instance, `type/list/Code.cs:19`). Facts of an assembly do not change: one scan per process is the cheap, local fix for a process that makes more than one root app (the test runner's apps, a server's roots); it does not help a CLI that makes one.
- **For one cold root app the lever is not scanning at run time:** the Library's tables (class names, families, kinds, marks, readers) are all derived from the assembly's metadata, which the source generator can write at build time; the scan then reads a table. Not tried; this is the idea.
- **A sub app, hello world:** cold, after its maker (the server app: 220–280 ms, JIT 76–91) it is born in **5 ms**, then the first goal load **186–210 ms (JIT 143–162)** and the first run **38–43 ms (JIT 35–40)**; the second sub app in the same process is born in 0.5 ms, loads in 0.4, runs in 1.6. Warm, a whole request app (born + load + run + dispose) of the write-out goal is **1.0 ms and 201 KB** (root app: ~20 ms and ~2.2 MB). A sub app is cheap to create, but it needs a maker, so a CLI run pays the root's cold start once whichever way it runs the goal; the first goal's load and first run are cold JIT in both.
