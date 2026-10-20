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

## 17. The memory's table and path's schemes: a birth at ~18.4 KB

| change | birth alone (4,000 traced) |
|---|---|
| start (§16) | ~21.5 KB |
| a memory's own table made at its first write (`Store`); a read of an empty memory makes nothing | ~20.1 KB |
| path's schemes (`file`, `http`, `https`) are kind classes (`path/scheme/file`, `http`, `https`), made when path's kinds are first asked, as `number.kind.int` is. Before, every birth added three schemes, and adding them made path hold all its kinds | **~18.4 KB** |

Timed against §15 (`692f4c990`) back to back under the same heavy load (18 – 25), 3 × 100 each: shared birth median
**100 – 141 µs vs 182 – 193 µs**, min **63 – 71 µs vs 100 – 103 µs**; fresh about the same (noise). A quiet-machine
median should land near ~60 – 70 µs (§15 measured ~88 at load ~9).

Surface move re-pinned on the playground: the three scheme classes added; `scheme.@this` unsealed (sealing isn't
pinned). C#: green except Wire's six `carried`. plang: the same 11 names.

## 16. Nodes, kinds' keys, a context's tables: a birth at ~21.5 KB

| change | birth alone (4,000 traced) |
|---|---|
| start (§15) | ~29.8 KB |
| an empty kind's key tables made whole, at their size, at the first key looked up after kinds came in — not grown and re-made per kind added (`kind.Key` puts one kind in, no LINQ iterators) | ~28.2 KB |
| a node (`app.goal`, `app.variable`, …) finds its concept's type when first asked; its settings class is read once per class (was `GetInterfaces()` per node per birth); its list made once without a `Lazy` + closure | ~25.8 KB |
| a context's keyed values, wrapper cache, deadline stack and running-bindings slot, and an actor's channel table, made when first used | **~21.5 KB** |

Birth + test mode + dispose: 117 → 110 KB. Times not taken: load 12 – 18 during the runs (shared median 92 – 217 µs,
noise). C#: green except Wire's six `carried` (baseline). plang: the same 11 names as before §15.

Left (~21.5 KB): type entries made on the asks of the birth (~7.5%: `actor`, `actor.list`, the store's, the call
list's…), the type list's own tables and its `_born` array (~4%), empty kinds made for full types (`BornKind`, ~3.7%),
`list`'s kinds enlisted because `actor.list` asks for `list<actor>` (its key lookup), the variable store's dictionary.

## 15. System variables made on first ask: a birth at ~88 µs, ~30 KB

`Now`, `NowUtc`, `GUID`, `MyIdentity`, `Identity` (every memory) and `!app`, `!context` (a context's own) are one
fixed table on the variable list (`variable/list/this.cs` `Computed`). A memory makes one the first time its name is
looked up (`Find`: a miss on the store asks the table), and makes all of them before anything walks the store
(`All`: names, listings, snapshots, clone, save/restore), so every walk sees what it saw before. One bit per entry
records "made": a set or remove of a system variable sticks, as before. `!app`/`!context` read the memory's live
context; the context says `Variable.Born()` instead of placing two Data. Writes that bypass a set (a clone, the
throw-time projection) go through `Place`, which marks a system name as made.

Two orderings leaned on the birth time: `GetAll` (newest first) put the system variables last because they were born
first. Made late, they'd sort first, so they are ordered last explicitly (`GetAll_OrderedByUpdated` caught it).
`GetChangedSince` skips them, because nothing sets them. Before, they counted as "changed" only for a `since` earlier
than the memory's birth.

| measure (3 runs × 100, same machine) | before | after |
|---|---|---|
| shared birth, median | 90 – 108 µs (load ~7) | **87 – 92 µs** (load ~9 – 10) |
| shared birth, min | 71 – 90 µs | 66 – 70 µs |
| fresh birth, median | 1,162 – 1,385 µs | 1,276 – 1,332 µs (noise) |
| birth + test mode + dispose, alloc | 121 KB | 117 KB |
| birth alone (4,000 traced) | ~34 KB | **~30 KB** |

C#: green except Wire's six `carried` snapshot tests, the same six red on the previous commit. plang: the same 11
names fail as on the previous commit (names diffed, not counted).

Left in a birth (~30 KB): type entries the app's own constructor asks for (`type.list[Type]` → `Born`, ~10%), the
empty kinds' key tables (`kind.Keys` iterator + dictionary, ~9%), each actor's channel table (~3.6%) and context
(cancellation source, stacks, the variable store's own dictionary, ~2.6%), the type list's tables.

## 14. Speed and memory, round on round: a birth at ~95 µs, ~34 KB

Each cut found by allocation sampling with call stacks over 4,000 shared births:

| change | per-birth effect |
|---|---|
| a lighter type object: the code's facts read through one reference (five copies gone — a flat copy); eight first-ask caches in one `Answers` object made when any is asked; `Spelled` without making the empty kind | ~60 → ~54 KB; every type object at run time is smaller |
| an actor's task list and permission view made when first asked; a context's id and trace made when first read; `!app`/`!context` placed directly (`Born`), no async set | ~54 → ~44 KB |
| a context's call list finds its frame types at the first push, makes its audit list at the first error | (in the above) |
| console channels wired when an actor's channels are first used (`Standard()`) | ~44 → ~38 KB |
| the store resolves its file when it opens; the system's standing grant is a verb list the permission view applies when made | ~38 → ~34 KB |

**Birth now** (3 runs × 100, load ~8): shared **~95 µs** median (min ~72); fresh ~1,200 µs (min ~895).
C# green but Wire's six (one load-sensitive timeout test, `ErrorInPlayTests`, failed once under the sweep and was green
on its own and in two full Modules runs); plang 277 / 11, the same 11. Surface moves re-pinned on this playground:
`type.Alias`/`Internal` lose `init`; the call list's ctor takes makers.

Left in a birth (~34 KB): the actors' system variables (`Now`, `NowUtc`, `GUID`, `MyIdentity`, `Identity`, `!app`,
`!context` — `DynamicData` with a computed cell each, ~12%), the ~20 type entries the app's own constructor asks for,
the type list's own tables, each actor's channel table and context (cancellation source, stacks).

## 13. The generator carries the attribute data: cold start ~1,500 µs

The generator now also writes, as code:
- **`FormatData`:** each `[Format]` class with its declarations, as `new FormatAttribute(...) { ... }` built from the
  attribute's own arguments.
- **`ActionData`:** each action class with its `[Action]`.
- **`Statics`:** each item class's `Alias`, `OwnedClrTypes`, `Internal` and `Offer`, read through delegates that call
  the static properties directly (own or inherited, as the reflection lookup found them; `Internal` only declared on
  the class itself). A generic class stays on reflection.

The facts read these instead of reflecting. `type.list.NameOf` reads a class's word from the identity cache.
`WrittenListsTests` (9 tests) checks the data equals what reflection reads: formats per class, each action's name and
cacheable, every type's statics through the facts versus reflection. Deletion-tested: writing every `Internal` as
`false` fails it. The emitter writes nothing for code without plang's attributes (the generator's own test fixtures)
and never fails the generator: any error means no lists, and the runtime walks as before.

| fact (cold) | §12 | now |
|---|---|---|
| format | 494 µs | 16 µs |
| module | 688 µs | 138 µs |
| type | 825 µs | 682 µs |
| **all facts** | ~3,400 µs | **~2,600 µs** |

Fresh birth: ~2,000 → **~1,300 – 1,700 µs** (min ~1,140). Shared: ~185 µs. C# green but Wire's six; plang 277 / 11.

Left: `kinds` ~1,000 µs (an instance per kind class and subclass kind, to learn keys the classes compute in code).

## 12. The generator lists the classes: cold start ~2,000 µs

`PLang.Generators/Emission/Written` emits `PLang.Generated.Written` for plang code: `typeof` lists of the item classes
and of the classes declaring `[Format]`, `[Choice]`, `[Action]`, or sitting in a `serializer` / `format` folder. The
lists are supersets. The runtime facts (`app.assembly`) read them instead of walking every class, and keep every rule
(naming, owners, sets) on the runtime side, so there's one source of truth. An assembly built without the generator
is walked as before. `WrittenListsTests` checks each list holds every class the facts' rules pick when walking; a
deletion test (dropping `[Choice]` from the generator) fails it.

Also: `type.Spelled`, a type's spelled kind asked without making its empty kind, used by the list's lookups.

| fact (cold) | §11 | now |
|---|---|---|
| type | 1,812 µs | 825 µs |
| format | 888 µs | 494 µs |
| choice | 629 µs | 74 µs |
| subclass | 450 µs | 131 µs |
| reader | 395 µs | 208 µs |
| module | 762 µs | 688 µs |
| kinds | 1,222 µs | 795 µs |
| **all facts** | ~6,400 µs | **~3,400 µs** |

Fresh birth (`new app` reading its own facts, warm JIT): ~4,100 → **~2,000 µs** (min ~1,440). Shared: ~190 µs.
C# green but Wire's six; plang 277 / 11, the same 11.

Left of a cold start: building attribute objects (142 `[Format]`, 143 `[Action]`), one instance per kind class to learn
its keys, each type's statics. The generator could carry that data too (attribute arguments are data, not rules).

## 11. Locked core opened (Ingi: nothing merges from here): a birth at ~175 µs

Units from here on: µs (1 ms = 1,000 µs; 1 µs = 1,000 ns).

| step | what changed |
|---|---|
| `assembly` off the app root | the app's `assembly` parameter goes to the type root; the type list holds the facts (`type.list.Code`) |
| readers shared | the facts' reader tables (folders' readers + closed sets' + subclasses') shared by every app; runtime tables made on first registration |
| format pairs shared | the facts list the `format/` pairs, made once; a pair list no longer walks the assembly |
| modules lazy | the app's ~40 modules made when a module is first asked |
| channel settings lazy | `Direction`, `Buffer`, `Mime` made when read (three class-built values per channel before) |
| code registry tables lazy | its three dictionaries made on first use |
| `Data.Properties` lazy | every value carried an empty bag; now made when asked, `HasProperties` for the wire writer |
| naming path | owner of a raw C# shape found via facts (only it made); no closure per `PlangName` call |
| fresh facts | reader facts 1,410 → 395 µs; kinds facts 343 → 252 KB |

**Birth now** (`new app` alone, 3 runs × 100, load ~8): shared **~175 µs** median (min ~146 µs), ~160 KB with
`Testing()` + `Dispose` (~63 KB for the birth itself by allocation trace). Fresh (cold facts, the CLI): ~4.1 ms.
C# green but Wire's six; plang 277 / 11, the same 11. Sweep: Types 7.9 s, Data 7.3, Runtime 9.9, Wire 15.3,
Modules 20.7.

**What's left of a cold start (~4 ms, warm JIT):** attribute scans over the ~950 written classes — `type` 1,812 µs
(`GetTypes`, names, each type's statics), `format` 888 µs, `choice` 629 µs, `module` 762 µs, `kinds` 1,222 µs (an
instance per kind class to learn its keys). By reflection these are near their floor. **The design that removes them**:
the source generator (it already sees every class) emits the code's facts at compile time — the types, formats,
choice sets, kind classes and their keys, actions, readers, pairs — so a cold start reads a table, not the assembly.

## 10. Type entries made on first ask: a birth at 0.3 ms, ~95 KB

Allocation sampling with call stacks (4,000 shared births) pointed at:

| what | share before | fix |
|---|---|---|
| `kept.list` in every module (and actor): an empty `ConcurrentDictionary` + lock each | ~12% | the store is made on the first add (one change in `kept.list` covers every holder) |
| the code registry's 17 built-in providers, their dictionaries, a throwaway `Ok()` each | ~7% | `RegisterDefaults` arms them; made when the registry is first asked |
| class-built types (`choice<ChannelDirection>`, `text`'s operator) reading `[PlangType]`/`[Format]` per value | ~3% | name and formats kept in the class identity cache `item` already had |
| the 131 type entries | 23% | made on first ask (below) |
| the facts' getters allocating their factory lambda on every read | 1.8% | a field check first |

**Type entries on first ask.** The list base (`type.item.list`) gets one private hook, `Fill()`, empty by default,
called before its rows are read or changed: `Now`, `Copy`, `Change`, `Stored`, `CountRaw`, `At`. The type list overrides
it. A lookup by name or class (the facts' index) makes just that entry, into the place the index points at. The first
read of the rows (a walk, a count, plang navigating `%!app.type.list%`) makes the rest and adds them all, in the facts'
order, before any type added later. Answers and order are unchanged; the surface golden doesn't move.
Load guards against re-entering itself on its own thread (adding another assembly's types reads the rows).

**Birth now** (`new app` alone, 3 runs × 100, load ~9): shared **0.3 ms** median (min 0.2); ~95 KB per birth by the
allocation trace (the timing test's 0.2 MB includes `Testing()` and `Dispose`). Fresh: 5.7 – 6.1 ms. C# green but
Wire's six; plang 277 / 11, the same 11; `plang --test` 9.0 s.

Left in a birth (~95 KB): type entries asked at birth (~16 asked by the app's own constructor: parent, datetime,
setting, text, path, …); ~40 module objects; choice and subclass readers registered (~30 small objects + dictionary
nodes); the actors' contexts, variable stores and channels; the reader registry's dictionaries.

## 9. Toward the floor: a birth at 0.4 ms, 0.2 MB (aimed at a server: thousands of users)

Each step below was found by measuring: `dotnet-trace` CPU sampling of a whole suite (callers summed by a script),
and allocation sampling of 2,000 shared births summed by object type.

| step | what was found | fix |
|---|---|---|
| fresh birth regressed (18 ms) | the facts made a throwaway format kind per `[Format]` to learn its keys | a format's keys come from its attribute |
| facts walks | 2,355 of PLang's 3,311 classes are compiler-made (closures, state machines) | the facts skip classes with `<` in the name |
| 9 threads read the same facts at once | a suite's (or a server's) first wave each read the facts in full (~2.6 s CPU) | each fact read once under one gate; the rest wait for that answer |
| reader registry | each app walked every class on its first read, and for each serializer folder walked all classes again; a static `Read` was called through `MethodInfo.Invoke` per read | readers come from the facts; a static `Read` is a delegate bound once |
| `text`'s implicit operator | every C# string turned into `text` read `text`'s `[Format]` and made a format kind (2% of a suite's CPU) | a class-built type reads its own format when its kind is first asked |
| empty kinds (14% of birth allocation) | every type entry was born with an empty kind, itself a full type object | made when the type's kind is first asked |
| binder delegates (13%) | each type object bound two delegates at birth, empty kinds included | bound on first use |
| `MemoryCache` (6%) | each app opened a named memory cache with expiry buckets and timers | opened when something is first cached |
| `Claims` iterators (6%) | a type's names walked by iterator three times while indexing | an array the facts read once |
| name index (11%) | each app built an immutable name and class index | the facts' index (name or class to the entry's place) over each app's entry array |

**Birth, `new app` alone** (3 runs × 100, load ~6):

| | median | CPU / birth (with Testing + Dispose) | alloc |
|---|---|---|---|
| shared facts | **0.4 ms** (min 0.3) | 0.8 ms | **0.2 MB** |
| fresh (the CLI's one app) | 6.2 ms | 8.5 ms | 1.1 MB |

**Suites** (the sweep, all at once, load ~5): Types 7.5 s, Data 7.6 s, Runtime 9.5 s, Wire 19 s, Modules 21.5 s (§7:
18 / 17 / 17 / — / 40). `plang --test` 8.2 s (17 – 18 before).

What a shared birth still allocates (~170 KB, by share): the 131 type entries 20%; `object[]` 7%; lock objects 7%;
`ConcurrentDictionary<string, data>` tables 7% (variable stores, made at birth at default capacity); the code
registry's `ConcurrentDictionary<string, ICode>` 3%; reflection at birth (`CustomAttributeRecord`, method stubs) 4%;
module objects 3%.

**Cleanup for the report (Ingi):** plang walks an assembly's classes (`Assembly.GetTypes`, .NET's
`RuntimeModule.GetDefinedTypes` underneath) from several places, each its own way. Each is a code fact and belongs in
the one facts object (`app.assembly`):
- `type.kind.pair.list.Find`: each format's write pairs, per app, on first output.
- `actor.setting.Index`: the setting classes.
- `module.list.type.query.Known`: a static walk.
- `choice.set.family`'s template branch: walks the classes for template kinds.
- `type.item.@this.NameOf(assembly, namespace)`: `GetType` per call.
- `type.list.@this.PlangName`'s `Owns` walk: every entry per raw C# shape named (not a class walk, but the same
  shape).

## 8. Made when asked: a birth at ~1.8 ms, 0.5 MB

What a shared birth made, with time and memory per section (load ~6, 100 births):

| section | before | after |
|---|---|---|
| type entries, made + indexed + added | 0.86 ms / 291 KB | 0.60 ms / 151 KB |
| kind classes, formats, choices, subclasses, guard | 1.7 ms / 360 KB | none; readers 0.03 ms / 6 KB |
| module (142 actions) | 1.16 ms / 363 KB | 0.08 ms / 34 KB |
| channels (6 stream channels) | 0.92 ms / 450 KB | 0.27 ms / 66 KB |
| Code, store, nodes, schemes | ~0.4 ms / ~90 KB | same |

What changed:
- **Channels:** a stream channel's 64 KB read-ahead buffer is made on its first read. An output channel never
  reads.
- **Modules:** a module is born holding its action classes and makes its actions when first asked.
  `module.Action` is the one `list<action>` (Ingi). `ActionNames`, `Contains`, `Count` and the dictionary are gone.
  Selecting by name walks the list by position, allocating nothing (the allocation test holds).
- **Entries:** the name and class index is built in one pass, so each entry's admit finds itself already recorded.
  Before, each entry rebuilt the immutable index four times.
- **Kinds:** a type's kinds (kind classes, formats, element kinds, choice sets, family subclasses) are held the
  first time any of them is asked. The empty kind asks its list (`Enlist(owner)`).
  - A lookup by key (MIME, extension, name) asks only the types the code's facts name for it, plus the types holding
    kinds the facts don't list (schemes, loaded assemblies), each through its own `kind[key]`. The index is a
    superset, so the answer is the type's own.
  - The guard (one MIME or extension per format), "no type reads this format", "not a closed set" and "one set name,
    one class" are checked once, when the facts are read.
  - Readers for choice sets and subclasses are still registered at birth, from the set names the classes declare.
  - A family set (`hash.kind`) reads its family type's kinds, not every type's.
- **Order:** actions now come in code order. The old order was the dictionary's hash order. Re-pinned, with the same
  lines reordered: `pick_golden.json` and the condition/file/screen pages.

Birth, `new app` alone (3 runs × 100, load 5–7):

| | median | alloc |
|---|---|---|
| shared facts | **1.7 – 1.8 ms** (min 1.5) | **0.5 MB** |
| fresh (reads its own facts) | 18 – 20 ms | 1.7 MB |

**Regression to fix next:** a fresh birth went from ~15 to ~18 ms. The facts now make one instance of each kind
class, format and subclass kind to learn its keys and check the guard, and every app that reads its own facts pays
that. That includes the CLI's one app. A format's keys are in its attribute, so formats need no instance. Kind
classes could declare theirs, or be read once per process.

C#: green except Wire's six. plang: 277 / 11, the same 11.

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
