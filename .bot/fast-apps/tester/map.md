# fast-apps — the map of test-speed (tester)

test-speed is in `/workspace/plang-test-speed`. Its write-up is `.bot/test-speed/tester/test-speed.md`; §N below is that file's section. Branch tip: cfb042cc1.

**Already in runtime2** (merged with item-doors): §5's four commits (e92111cc4 `[Choice]`, dd899b865 type facts on first ask, 328799fb1 types by class through an index, 81159279d the guard's clash as a lookup) and 238c4994a. Everything below is **not** in runtime2.

All measurements were taken on the playground: Debug unless it says Release, on a shared machine at the load given in each section. Use them as ratios between rows, not as absolute numbers.

## The changes

Locked surfaces named in the table:
- **app**: the app's members and constructor.
- **type**: the type system (`type`, `type.list`, `kind`, `property`, `item`'s base).
- **data**
- **module**: the module registry and catalog.
- **call**: call and variable memory.
- **events**: the event door.
- **gen**: the generator.

"Surface" means the surface golden moved. "private" means it didn't.

### A. The code's facts, read once per process (§6, §7, §9, §11). The big decision; see the last section.

| | |
|---|---|
| What | `app.assembly.@this` holds what the code says: the types and their statics, kind classes, `[Format]` and `[Choice]` classes, family subclasses, actions, readers and format pairs. It is read once under one gate per fact, and every app born from the same code takes it. It skips compiler-made classes (2,355 of 3,311 have `<` in their name). From §11 on, the type list holds it (`type.list.Code`), not the app. |
| Win | Birth went from **26–29 ms to 3.4–3.7 ms** (§7) and **0.4 ms / 0.2 MB** (§9). Cold start, for the CLI's one app or the first test, is 6 ms. The suites (§7, loaded machine): Types 14.6 → 8.8 s, Runtime 29.7 → 19.1 s, Modules 39.8 → 33.5 s, `plang --test` 17 → 15.6 s. By §9: Types 7.5, Data 7.6, Runtime 9.5, Wire 19, Modules 21.5 s; `plang --test` 8.2 s. |
| Commits | fe7d134a9 (§7), 1abbafeee, 59585ff69, 509d37ee7 (§9), d3afad17b, 10a860844 (§11) |
| Files | `assembly/this.cs` (new), `app/this.cs`, `type/this.cs`, `type/list/{this,Registry}.cs`, `type/reader/this.cs`, `type/kind/pair/list/this.cs`, `type/kind/empty/this.cs`, `module/this.cs`, `test/list/this.cs`. Tests: `PLang.Tests/Shared/Session.cs` (one code per test process), and every `new app(…)` goes through `Session.App(…)` (556 files, mechanical). |
| Locked | **app**: the constructor gains `assembly`. **type** (internal only): `type.list.Code`. |
| Depends on | nothing. Everything below assumes it. |
| My read | The holder was the open part, and `assembly` is a placeholder name. `Session` is a static in test code. With the snapshot plan's parent the static goes: the session is one frozen parent app. The facts object keeps every rule on the runtime side, and the generator's lists (C) are only supersets it reads, so there is one source of truth. I'd keep that. |

### B. What a birth makes, only when asked (§8, §10, §11, §14 – §17, §24)

| | |
|---|---|
| What | A birth stops making what nothing has asked for yet. These are made on first ask: modules and their 142 actions, a type's kinds, type entries, the 17 code providers, channel settings, stream buffers, `Data.Properties`, an actor's and a context's parts, console channels, the store's file, system variables, a node's type, the memory's table, and the app's id/folder/start. Path's schemes become kind classes. |
| Win | Birth went from 3.5 ms / 1.7 MB (§7) to 1.8 ms / 0.5 MB (§8), 0.3 ms / 95 KB (§10), 175 µs / 63 KB (§11), 95 µs / 34 KB (§14), 88 µs / 30 KB (§15), 21.5 KB (§16), 18.4 KB (§17) and 5.8 KB (§24, bare request app). |
| Commits | bda3f9768, 60cec2c03 (§8); 7afa504a7, f1845c2b3 (§10); 0f7d3e7fe (§11); de1b68545, a14283c66, 191efc570, 47ba94995 (§14); 692f4c990 (§15); 6850fd9d9 (§16); c0ed8587e (§17); 230a615ae, 744e974a9 (§24) |
| Files | `module/this.cs`, `module/code/*`, `type/list/*`, `type/kind/{this,empty/this}.cs`, `type/item/list/this.cs` (the `Fill()` hook), `type/item/kept/list/this.cs`, `channel/*`, `data/this.cs`, `actor/{this,context,permission,list}/*`, `call/list/this.cs`, `type/item/variable/list/*`, `type/node/this.cs`, `type/item/path/scheme/*` (new), `store/sqlite/this.cs`, `parent/this.cs`, `app/this.cs` |
| Locked | **module**: `module.Action` becomes `list<action>`, plang-visible as `.action : list<action>` (bda3f9768). **type**: `type.Alias` and `Internal` lose `init` (de1b68545); three new kind classes `path.scheme.file`, `http`, `https`, with `scheme` unsealed (c0ed8587e); the `Fill()` hook on `type.item.list` (private, but on a core collection's base). **call**: the call list's constructor takes makers (`Func<…>`) instead of values (a14283c66). **data**: `Properties` made when asked; `HasProperties` stays internal (private). |
| Depends on | A for the numbers. Taken alone, each change is still a smaller birth. |
| My read | Most of this is plain lazy fields, and I'd take it as is. Three smells:<br>(1) `Fill()` is a hook on the list base for one subclass, the type list, and every list pays a virtual call. If the type list is shared (F, or the parent), entries are made once per process and lazy entries are not needed. **I'd drop §10's first-ask entries rather than port them.**<br>(2) §15 orders system variables last in `GetAll` explicitly, because they are now born late. That is a special case, though it is tested.<br>(3) §14's call-list makers exist only because an item may not hold a context (`NoItem_DeclaresAContextFieldOrProperty`). They are closures standing in for a reference. |

### C. The generator writes the facts (§12, §13)

| | |
|---|---|
| What | `PLang.Generators/Emission/Written` emits `PLang.Generated.Written`. It lists the item classes and the `[Format]`/`[Choice]`/`[Action]`/serializer classes, carries `FormatData`, `ActionData` and each type's statics as code, and the facts read these instead of reflecting. Code built without the generator is walked as before. `WrittenListsTests` checks the written lists equal what reflection reads. |
| Win | **Cold start only**: the facts went ~6,400 → ~3,400 → ~2,600 µs, and a fresh birth 4.1 → 1.3–1.7 ms. Nothing per app once A shares the facts. Suites: one cold start per process, so about 1 ms. |
| Commits | ae5ec0137 (§12), 08e8e5560 (§13) |
| Files | `PLang.Generators/Emission/Written/this.cs` (new), `PLang.Generators/this.cs`, `assembly/this.cs`, `type/list/Registry.cs` |
| Locked | **gen**: a new emission. It writes nothing for code without plang's attributes, and any error means no lists. |
| Depends on | A |
| My read | Sound, and the right long-term place for code facts. But it is the CLI's start and a server's first request, not the suites. |

### D. 157: a format binds its own encode/decode (b54acfcd5)

| | |
|---|---|
| What | A format kind binds T's own `Encode`/`Decode` on its first write or read, once per T for the process. The registry binds nothing. |
| Win | ~2 ms per birth **before A** (28–31 → 26–29 ms). After A it is folded into the shared facts: about nothing per birth. |
| Files | `type/kind/{this,this.Generic,format/this}.cs`, `type/item/{IEncode,IDecode}.cs`, `type/list/Registry.cs`, `type/this.cs` |
| Locked | **type**: `kind`'s constructor loses `encode`/`decode`, the `Encoder`/`Decoder` delegate types go, and the format kind overrides `Encode`/`Decode`. Approved: decision 157. |
| Depends on | nothing |
| My read | The shape is good (a format owns its encode/decode instead of a registry handing delegates in). It is a cleanup with a small win, not a speed lever after A. |

### E. The run path: goals shared, a list's reads without its lock, fewer allocations (§18 – §20, §22)

| | |
|---|---|
| What | The changes, by section:<br>**§18:**<br>- A `.pr` is read once per code and folder by a private reader app. Each app adds that goal to its own list, and a stat (modified + size) keeps it fresh.<br>- A list's positional reads (`list[i]`, `CountRaw`) read its snapshot, not its lock.<br>- An in-memory store nothing has written answers empty without opening SQLite.<br>- Class facts are read once per process: choice sets, action properties, setting paths and defaults.<br>- Call frames make their parts on first use.<br>**§19:**<br>- `Data` skips the json parser for a non-json value.<br>- `type.Empty` has no closure.<br>- Depth is checked as an int.<br>- Settings answer an unset option at once.<br>**§20:**<br>- A catalog action keeps its handler shell.<br>- Handler slots are made when first read.<br>- Path resolution: the app's anchors once per folder, and no stat for a path with one place.<br>- A shared goal is fresh for 1 s.<br>- `Attempt`/`Dispatch` are `ValueTask`.<br>**§22:**<br>- The settings' class index is built once per app.<br>- The call chain is built only for an error.<br>- A write offers the previous value only to a value that keeps it.<br>- `type.list.Of(name, kind)` replaces the probe types on the run path.<br>- Step/action `Start` are `ValueTask`. |
| Win | Small app (birth → load → run → dispose): **6.1 → 1.4 ms, 780 → 290 KB** (§18), then ~1.1 ms / 195 KB (§20) and 171 KB Debug / 121 KB Release (§22). The goal load went 2.1 ms / 223 KB → 0.2 ms / 25 KB. The list-snapshot read was the round's largest time cut (3.2 → 1.5 ms), and the empty store cut run CPU by 20 %. |
| Commits | 1607da883, 21015b601, 7780135d9, 7ab006291, c0634c743 (§18); 339f2ce82 (§19); eb0d50c38, 5aa79ebf2, 3d90ec3f2, 5c0c9a4de (§20); f6157f79e, f79637586, 9f9de29f0, 303a8f8cc (§22) |
| Files | `goal/program/this.cs` (new), `goal/{this,step/*}`, `type/item/list/this.cs`, `store/sqlite/this.cs`, `assembly/this.cs`, `type/item/choice/set/*`, `type/property/*`, `actor/setting/this.cs`, `call/{this,list/this}.cs`, `data/this.cs`, `type/item/path/*`, `type/item/{bool,dict,list,text}/serializer/Reader.cs`, `type/item/{number,size,source}`, `task/this.cs`, `PLang.Generators/Emission/{Action,Property/Data}/this.cs` |
| Locked | Private by the surface golden, but inside locked surfaces: **gen** (eb0d50c38, f79637586 and 9f9de29f0 change what the handlers' emission writes); **type** (`type.list.Of`, internal); **data** (the parser skip, private); **call** (frames, private). The step and action `Start` → `ValueTask` didn't move the golden, but four test files changed with it. |
| Depends on | A. Goals shared needs one code per process. |
| My read | Three smells:<br>(1) The **goal reader app** is a hidden second app per code and folder. With a parent, the parent's loaded goals *are* the frozen goals; I'd use those instead.<br>(2) The **1 s freshness window** (3d90ec3f2) is a time-based cache: a goal changed within a second reads stale. I'd drop it and keep the stat.<br>(3) **"No stat for a path with one place"** changes what an `exists` answer costs, so it needs a second look.<br>The list-snapshot read is a plain correctness-safe win; take it first. The empty-store answer is a small fork, and it's justified. |

### F. One type list per code, shared (§19, §23)

| | |
|---|---|
| What | `new app(…, assembly: code, shareTypes: true)` takes the one type list every app made from `code` shares. A shared list refuses additions (`TypesShared`), and an app that loads code is born with its own. What an app binds on a shared object (a goal, step or action, a type entry) is held on the binding actor's context (`held.Own`, `held.On`), not on the object (Ingi's idea, §19). |
| Win | Condition birth 37.5 → **24.2 KB**, small app 1.32 → 1.14 ms (Debug) / 0.91 ms (Release). Parallel allocation per request fell 16 – 18 %. §19's change also fixes a leak: before it, every request app binding on a shared goal added to it for the life of the process. |
| Commits | 909bc8e84 (§19 held), bf5982b97, f967622d8 (§23) |
| Files | `event/{this,held/this}.cs`, `goal/{this,step/*}`, `module/{on/event.cs,debug/this.cs}`, `test/{this,list/this,Coverage}.cs`, `type/item/this.cs`, `type/list/Registry.cs`, `type/node/root/this.cs`, `assembly/this.cs`, `app/this.cs` |
| Locked | **app**: the constructor gains `shareTypes`. **events**: the event levels read the asker's hold, and a new internal type `app.event.held` is on the type list (golden: `type app.event.held`). **type** (internal): `type.list.IsShared`, `item.Keeps`. |
| Depends on | A, and §19 (`held`) for F |
| My read | `held` is the part I'd keep whole: it is what makes any sharing correct, parent or not. `shareTypes` is a bool that forks the birth (a **fork**): with the parent, a child of a frozen parent shares and nothing needs the flag. Tests guard the sharing (`ABindingOnASharedGoal_IsTheBindingAppsOwn`, `ABindingOnASharedTypeEntry_IsTheBindingAppsOwn`, `ASharedTypeList_RefusesAnAddition_SayingWhy`), each red under mutation. |

### G. Server-side, not suite time (§29, §30), and large data (§25 – §27)

| | |
|---|---|
| §29 c1582dcf5 | **A real leak:** an app's step cache (`MemoryCache`) was never disposed, keeping 73.5 KB per request with `cache for …` (223 MB after 3k requests). Fix: `ICache : IDisposable`, and the app disposes its cache (`CacheLifetimeTests`). Also: the sqlite pool is cleared when the last store on a database lets go. Locked: none (`ICache` is a module's interface). **Take it regardless of speed.** |
| §30 cfb042cc1 | The empty kind's key lookup read without the type's gate: a published immutable table, dropped on `Add` (`KindLookupTests`). Shared types make it a cross-app lock on the hot path. Locked: none (private to `kind/empty`). Needed once types are shared (F or the parent). |
| §25 – §27 | A file streams through a save (261 MB: +785 MB → +2.5 MB), and `is empty` answers from the first byte. Not speed. Locked: **type**: `IVerbatim` on binary/image/text, content's `Format` becomes private protected (4c85abb82). A separate branch's worth. |

## Ranked: win per locked-core cost

| # | Set | Win | Locked cost |
|---|---|---|---|
| 1 | **A**, the facts once per process | 26 ms → 3.5 ms a birth; the base of every suite cut | one **app** constructor parameter, which the parent door replaces |
| 2 | **E**: list-snapshot read, empty store, goals shared, class facts once | `plang --test` ~8 → ~5 s; small app 6 → 1.4 ms | private; **gen** for the handler-emission trims, which can wait |
| 3 | **B**, minus §10's entries-on-first-ask | birth 3.5 ms → ~0.1 ms | **module** `list<action>`; **type** `Alias`/`Internal` init and the scheme classes; **call** list makers |
| 4 | **G §29/§30** | a leak fixed; no cross-app lock | none |
| 5 | **F**, `held` | correct sharing, a leak fixed, −35 % birth bytes | **events** (`held`), **app** (`shareTypes`, which the parent replaces) |
| 6 | **D**, 157 | ~0 after A (2 ms before) | **type**, already approved (157) |
| 7 | **C**, the generator | cold start only (~2.5 ms) | **gen** |

**What gets the suites to test-speed's numbers.** §21 measured the C# sweep at 29 – 31 s (parent 137 – 215 s) and `plang --test` at 5.0 – 5.4 s (parent 24 – 31 s):
- **C# sweep ~30 s** needs **A + B**. By §11, A and B had the suites at Types 7.9, Data 7.3, Runtime 9.9, Wire 15.3 and Modules 20.7 s, against §21's final 7 / 6 / 8 / 15 – 16 / 18 – 20.
- **`plang --test` ~5 s** also needs **E's §18 four**. After A and B it was 8.2 – 9.0 s (§9, §10). The drop to ~5 came in §18: goals shared, the list-snapshot read, the empty store and class facts once.
- **Not needed for the suites:** C, D, F, §19 – §24's trims, G. They are for a server and the CLI.

Uncertainty: each intermediate suite figure was taken at a different load. Only §21 is back to back against the parent.

## The big decision: who holds the code's facts

On test-speed it is **option 1**: the app is born holding its code (`new app(path, …, assembly: code)`). Tests use one `Session.Code` per process, and `plang --test` hands the runner app's `assembly` to each test's app. §23 adds the shared type list with a `shareTypes` flag.

**The snapshot plan's holder (`new app.@this(parent)`, a child sharing a frozen parent's parts by reference) fits what I built.** The facts are read-only and per code, so a child taking its parent's by reference is exactly A, with the parent in place of the `assembly` argument:
- The test session's static goes: one parent per test process.
- `plang --test`'s runner app is the natural parent: on runtime2 it makes each test's app at `test/list/this.cs:178`.
- The goals the parent loaded are E's shared goals, without the hidden reader app.
- `held` (F) is what keeps a child's bindings out of the parent and its siblings. That is the plan's "event bindings at app scope, A ✓ in memory": the child's dirt is its context's holds, and a reset drops them.

**Where it costs**, which is the plan's copy on write for types and modules:
- **Copy on write for the type list was tried and rejected (§23).** Values born before the copy hold the parent's entries, while values born after hold the child's. Type identity (`type == type`) then splits inside one app.
- What worked is **frozen and refusing**. A shared list refuses an addition (`TypesShared`), and a program that will add types or load code is born with its own list. The runner decides that from the program: on test-speed, the two `code.load` tests.
- So for types and modules, **a child either shares the parent's list whole or is born with its own**. The decision is made at birth, not copied on first change.
- Copy on write fits the plan's other A parts as written: the settings' run layer, code providers, loaded goals and bindings (already per context through `held`).

**Two naming notes for the architect:**
- **The name `parent` is already taken.** `app.parent` is the app that started this process, a cross-process link (`call goal X in %!app.parent%`). The plan's `new app.@this(parent)` is an in-process frozen app. Two meanings for one word on the locked app surface would need a decision.
- **The child keeps its own folder.** A test's app has its own root, while the code is the parent's. So the door is likely `new app.@this(path, parent)`, not `(parent)` alone.

What it would cost over test-speed: the constructor parameter becomes the parent instead of `assembly`/`shareTypes`, a mechanical change. Also `Session.App` becomes `Session.Parent`'s child, and `test/list` passes its own app. The facts, `held`, and the frozen list carry over unchanged.
