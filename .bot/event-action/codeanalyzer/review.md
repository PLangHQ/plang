# event-action review: 6703697fb (runtime2) .. 3266637da

Asked by plang-coder, confirmed by plang-architect. Patterns named by number (obp-patterns.md): 11 lazy, 12 one answer, 3 a choice belongs on the element, 5 a birth fact not a search, 7 one door. Plus Ingi's return-early rule.
Read whole: `event/list`, `event/on/{this,side,before,after,empty}`, `event/{this,call,clause,code,inherited}`, `goal/step/action/this.cs` (Start, Attempt), `module/variable/set.cs` (lines 100–340). Read as diffs: the `Level` removals (goal, step), `type/this.cs` Create, `origin`, `snapshot/this.cs`, `variable/list`, `channel`, `debug`, `test`, `module/on/*`, `goal/pattern`, `Glob`. Not read whole: `Executor.cs`, `goal/this.cs` beyond the diff, `set.cs` 1–100.

## Verdict: FAIL, the coder next (one item to rule, the rest to fix)

## Bench (PGO off, `BENCH_SKIP="build sweep ptest"`, load 17–22; bytes do not move with load)

| | runtime2 6703697fb (parent) | event-action 3266637da | band |
|---|---|---|---|
| sub birth | 99 KB | 102 KB | ±5, in |
| root birth | 2349 KB | 2361 KB | ±25, in |
| sub request | 496 KB | **549 KB (+53)** | ±10, **out** |
| root request | 3840 KB | **4429 KB (+589)** | ±120, **out** |
| held (sub / root) | 1.6→2.0 flat | 1.6→2.2 flat / 0.1→2.1 flat | 0.2 MB, in |

ptest and sweep not run by me (the coder's gate: C# green but an HttpPathTests port race; plang 316).

## Findings

### 1. Pattern 11 / 5: every request app pays the system-events load, even where there is nothing to load (blocking until ruled)
Probe (`Bench events child`, a sub app per request, bytes on the thread, 500 apps): app 99.5 KB, `System.@event.Loaded()` **50.6 KB**, `User.@event.Loaded()` **28.0 KB**, goal load 36 KB, goal run 334 KB. The test root has no `system/event/` folder, so the 78 KB is `Run` (`event/list/this.cs:191–211`): `folder.Exists` and the context, path and AuthGate work behind it, twice (once per actor), plus `origin.@event(role)`. The sub app shares its maker's folder, so the answer is the maker's, already known. That is +53 KB net on the sub request (the lazy form saves ~25 KB elsewhere: no birth fill).
Root: user.load alone is 123 KB (against 28 in a sub app), and each root request app finds and runs `os/system/event/Hello.goal` (a .pr load and a goal run per actor per app). The +589 KB is the by-design price of decision 16 (system events on by default), but 16 does not name a price. Not the lazy shape (the shape is right); the cost of the question is the finding.
Fix shape, the coder's pick: a sub app asks the question of the folder its maker already answered (pattern 5, a birth fact, not a search): the folder's file list is read once by the app that owns the folder, and a sub app over the same folder takes it from there. Not a fast path beside the door: the same `Run`, fed the one answer.
Ruling needed: if the architect accepts the price of 16 at this size, say so in the decisions and I record the band as explained. Held memory is flat, so there is no leak.

### 2. Pattern 12: `actor.event` is a second accessor beside the `app.event(context)` door (PLAUSIBLE)
`app.@event(context)` loads, then answers `actor.@event` (`app/this.cs:134–140`). `actor.@event` is a public member too (`actor/this.cs:50`), and its `list` (`event/list/this.cs:49`, `[LlmBuilder]`) answers `_events` without `Loaded()`, as do `Items()` and `Of()`. The `list` doc says "`%!app.event` loads them first": true only through `app.event`. A path `%!app.actor.user.event.list%` would read an unloaded list. Fix: `list` loads, or is reached only through the door. I did not run the navigation.

### 3. Pattern 11, noted: the snapshot loads every actor before it reads (`snapshot/this.cs:48–50`)
I agree with decision 60 for the reason given (the setter is sync; a sync caller above it is the snapshot's own first ask). One correction to "only two actors": `app.actor.list.Items()` is every actor, so a snapshot read pays one load per actor the app has. Low. `Loaded()` as the load's one door: agree. It has three outside callers (`app.@event`, `snapshot`, `origin.@event`), each a first asker, and `On`/`Add` call it; I found no second answer. One note: `lock (_gate) _load ??= Load()` (`list/this.cs:169`) runs `Load`'s synchronous part inside the lock; a `TaskCompletionSource` or `Lazy<Task>` holds no lock while it runs. Low.

### 4. Pattern 12: "is this a moment's name?" is answered at three sites, and "which moments exist?" four times
- Name: `on.Add` (`event/on/this.cs:102`, `Core.Contains(name) || _names.Contains(name)`), `empty.this[]` (`event/on/empty.cs`, the same expression with `Declared`), `Get` (`this[key] != null`, then `Add(key)` asks again), and `Names`/`Declared`/`_names` as three members for one set (older: my finding 7 of snapshot 1268c81f7; the branch touches the file, so it is fixed here). One member answers it (`this[name]` null or not); `Add` and `Get` use it; `Declared` goes.
- Moments: `new[] { this }.Concat(_moments?.Values ?? [])` in `Remove`, `IsSaved`, `Write`, and a variant in `IsEmpty` (`on/this.cs:165,173,183,194`). One member `Moments` (the start and the named ones).
- `event/on/this.cs:158–160`: two stacked `<summary>` blocks on `Add(event)`; the first belongs to nothing.

### 5. Pattern 12 / cost: `Start` and `Attempt` each ask `On(Catalog)`, and `Catalog` is a lookup each time
`goal/step/action/this.cs:191` and `:220`; `Catalog => Module[Name] ?? this` (`:93`) runs at each. A retry asks again. Per action, two list asks and two catalog lookups where one is enough: `Attempt` takes the `on` (or `Catalog` is read once into the frame). Cost: not measured separately (inside the 334 KB goal run).

### 6. `module/variable/set.cs` (89202009b; read whole from 100): the read-once fix is half applied
- Four names for one value: `Value` (the parameter), `value` (`given ?? Value`, `:152`), `given`, `incoming` (`:192`): `value` and `Value` differ by case only (creative name).
- After the fix the typed branch still reads the *reference's* carrier in seven places: `Value.RawUntouched` (`:243,250`), `Value.Type` (`:243,258`), `Value.Is(type)` (`:260,270`), `Value.Value()` (`:250`: reads through the reference again), `CopyProperties(Value, …)` (`:313`), while `incoming`/`value` carry the Data it names. Same value, two readers: PLAUSIBLE (Pass 4.5 tell 14, asymmetry). A test: `set %y% = %x%, as <type>` where `%x%` holds a typed value or a lazy path.
- Return early (Ingi's rule): `Start` wraps 145 lines in `if (typeValue != null) { … }` (`:171–315`), whose every path returns, with the untyped write left as the tail (`:323`). Flip: `if (typeValue == null) return await name.Set(Binding(name, value), Context);` and the block loses a level.
- The typed branch writes through `Context.Variable.Set(typedData)` (`:247,314`), the untyped through `name.Set(Binding(...))` (`:323`): two doors for the write. Whether `set %x.a% = 1, as number` reaches the member is the question; PLAUSIBLE.
- `Build()` (`:103`): the comment names `.bot/context-never-null/...` (a task path in a comment). Older than the branch.

### 7. Pattern 12, low
- `Named(moment, name)` (`variable/list/this.cs`) answers with `moment.item as variable ?? parse`: an `as` inside an answer.
- The window list reads `empty.Of(GetType()).Names` from outside, and `module/on/event.cs` builds the refusal from `empty.Of(typeof(item)).NoEvent(path)`: a shared instance used as an error factory (stray helper).
- `Unread(...)` exists twice with the same key and status (`list/this.cs:288`, `call/this.cs:98`).
- Variable set, unmeasured: `On(name)` takes the actor's global `_gate` on every miss (a set of a name no event is bound on) and asks two doors (`list/this.cs:73–82`). `_named` as a concurrent dictionary read without the lock would not. Parallel flows contend.
- A new `Glob` is made per permission check (`permission/this.cs`), and `Matches` allocates a `bool[]` per glob piece; the pattern's `Has` runs it for each goal's list made.

### Checked and clean
- Locked core: `Has`, `IsMany`, `Is(pattern)` on item's base (decisions 39, 42, 45 are Ingi's); `actor.@event`, `app.@event(context)` (decisions 3–5), `on.event` without `Scope` (decision 3, "scope can go"). The surface golden is the tester's to re-pin.
- Early return elsewhere in the new files: no other guard wraps the rest of a method.
- Eager placement: no fill at start, bind or birth; `Run` asks on first ask. System events are not loaded by `Executor` or `Launch`.
- Not re-raised: `SystemGoalsLeftAlone` 10 against 7 (known); `goal.pattern` as a goal that refuses `Start` (decision 42).
- Deletion test: not run (no mutation this round).

## For the coder
1 (the load's cost, the architect's ruling first), 4, 5, 6; 2 and 7 as time allows. 3 is noted.

---

# Recheck after event-action 66 (2d10af80f, 4f960bba3)

Bench (PGO off, load 11): sub birth 103 KB (+4, in band), root birth 2366 KB (in), **sub request 524 KB** (parent 496, before the fix 549), root request 4434 KB (accepted by 66 as Hello's price: band explained), held flat (1.6→2.2, 0.1→2.1).

| # | Status | Notes |
|---|---|---|
| 1 | **half closed** | A sub app over its maker's folder now reads no folder, and the setting is not read (`Run` returns on `folders.Count == 0`): the first-ask load fell from 78 KB to 15 KB (system 7.5, user 7.4 by `Bench events child`). The sub request is still **+28 KB over the parent** (band ±10). Of the 28: ~13 KB is the same minting this load does (kind.empty 2.6, RuntimeMethodInfoStub 2.1, CustomAttributeRecord 1.0, FormatAttribute 1.0, type 0.8, kind.format 0.55 … by allocation ticks: a type and its kind made, attributes read by reflection, on a first ask: call-cost idea 2), the other ~13 KB sits in the goal's load and run (45 KB and 362 KB now; the parent's split is not measured), where the goal's, steps' and actions' lists are made on their first ask (decision 10's price); I did not attribute it further, so that half is an inference. Low note: the folder test is `string.Equals(..., Ordinal)` on `Absolute`; the file system has a root comparison (`path.RootComparison`), so a Windows root differing by case would read the folder twice. |
| 2 | closed | `actor.@event` is internal. |
| 3 | closed | `_load` is a claimed `TaskCompletionSource`; `Load` answers on it in a `try`, the failure rides to every asker; nothing held while it runs. |
| 4 | closed | `Declared` gone; `this[name]` is the one answer (`Add` and `Get` ask it); one `Moments`; the stacked summary fixed. `Add(name)` asks `this[name]` and `Made(name)` looks the name up again: two lookups of one answer, low. |
| 5 | closed (retry open with the architect) | `Start` hands its `on` to `Attempt`; a retry asks once itself. |
| 6 | closed | `set.cs` read whole: one local (`given`) and no `Value` reads after it, one write door (`name.Set(Binding(name, …))`, the typed branch too), the early return, the task path out of `Build`'s comment. Low: `Binding(name, typedData)` copies a Data made one line before; `named` in `Value.Given() is { } named` is a creative name for a Data. |
| 7 | closed | one `Unread` on the event base; `Named(name)` has no `as`; the refusal comes from the reached item's own list (`On(reached.Peek())`); the window list asks `On(this)[what]`; `Glob` made once at the grant's birth; `_named` is lock-free to read (`_variable.TryGetValue`, then `_named.TryGetValue`; the lock only to make a name's list the first time). Unfixed and still low: `Glob.Matches` allocates a `bool[]` per piece. |

Verdict v2: **FAIL, narrow.** 2–7 are closed. The sub request is +28 KB over the parent: 13 KB of it is decision 10's lists (explained); 13 KB is the type/kind minting on a first ask, which is the call-cost job's idea 2 (one type found, not made) and fixes the sub request, the root, and every action at once. The architect's choice: fix the mint now (a refactor of the type system's private part, and the largest saving in `call-cost.md`), or rule the 13 KB as the price of the lists' first ask and record the band as explained by 10 and 66.

**Ruling (architect, event-action 69):** the sub-request band (+28 KB at 2d10af80f) is recorded as explained: ~13 KB the per-actor lists made on first ask (decisions 10, 34), ~15 KB the first ask's type and kind minting, an older waste fixed as call-cost idea 2 in its own branch with its own review. The lows (`origin.Events` with `RootComparison`, `Add(name)` one lookup, `Binding` takes the Data as made, `named` renamed) go to the coder; I look at them without a bench and write PASS.

---

# The four lows (584fef19c), no bench — PASS

- `origin.Events` compares roots with `path.RootComparison`: closed.
- `on.Add(name)`: one lookup, closed. One low note (pattern 12): it decides by a type test inside the answer (`empty => Made(name)`), which is an `is X` on the kind; the OBP form is the shared moment answering "I stand in" itself (a virtual), so `Add` has no type test. Not blocking: one site, and the old form looked the name up twice.
- `set.cs`: the typed Data is made under `name.Code.Root.Name` and written as made through `name.Set(typedData)`; for a path target the container holds it under its own key as before. The read value's local is `binding`. Closed.
- Sub-request band (+28 KB): explained by 69.

Verdict: **PASS**, the tester next.

---

# Review v4 — 5d0ac4382..86d90db97 (stages 6–8, variable events on one store, the equality sweep)

Patterns first: 12 (one answer), 11 (lazy), 3 (a choice on the element), 5 (a birth fact), and the new tell (a value answers equality). Every touched file read whole; the diff 29 files in `PLang/`.

## Bench (`Tools/Bench/run.sh`, birth + request; load 7.2, bytes are load-independent)

| | Sub birth | Root birth | Sub request | Root request | Held |
|---|---|---|---|---|---|
| runtime2 6703697fb (parent) | 99 KB | 2349 | 496 | 3840 | flat |
| event-action 2d10af80f (my last) | 103 | 2366 | 524 | 4434 | flat |
| **event-action 86d90db97** | **101** | **2267** | **441** | **3898** | flat (2.2 MB) |

Everything is inside its band against the parent: sub birth +2 (±5), root birth −82 (below), sub request −55 (below), root request +58 (±120). Against my last row: the sub request −83 KB and the root request −536 KB. The type lookup keeping its answer (74, call-cost idea 2) and the variable events on one store (73) are what the rows show; the earlier +28 KB the architect ruled as explained (69) is gone. No bench finding. Times were measured at load 7.2 and are not compared.

## Findings

1. **Pattern 12 — the same fork written twice, and a second door for "which app finds a caller's goal". BLOCKING (narrow).**
   - `goal/this.Item.cs:94–97` (`Select`): `asker.call.Goal is { } caller ? await caller.Find(name, asker) : await asker.App.goal.list.Find(name, null)`.
   - `event/call/this.cs:72`: `action.Step?.Goal is { } binder ? await binder.Find(called, Actor.Context) : await Actor.App.goal.list.Find(called, null)`.
   - The question ("a call from this goal finds its goal in which app?") is answered by `goal.Find(name, asker)` (`goal/this.cs:64`, `(App ?? asker.App).goal.list.Find(name, this)`) and, beside it, by `goal.list.Find(name, caller)` called directly at `module/goal/call.cs:47` (Build), `goal/step/action/this.Callee.cs:17`, `module/screen/.../display/this.cs:278`, `module/browser/.../this.cs:64`, `.../screen/this.cs:221`. The no-caller branch is the fork each site writes for itself.
   - OBP form: one door. The caller-or-none ask lives once, on the goal as a static (`goal.Find(name, caller, asker)`: caller's app, else the asker's) or as the list's own overload taking the asker; the two runtime sites (`Select`, `event.call.Start`) call it and lose their ternary. For the five direct sites, either they move to it, or the architect rules they stay on purpose (build time and the os's `/system/…` goals, where the caller's app and the asker's are the same) and says so in the doc comment of the door.
   - Fix is the coder's (code). Small: two sites collapse, five are decided.

2. **Pattern 11 — the variable is made at every set, before anyone asked. Low–medium; tied to decision 73, so a note for the architect.**
   - `type/item/variable/list/this.cs` `Named(name)` runs at every `Set`, `GetOrAdd`, `Replace` and `Remove`: a `parser.Whole` (a variable, its code, a hop) is made whether or not any event is bound. The old shape made it only when something was bound. The architect's figure is 720 B per parse; the bench shows the net is below the parent's, so this is not a cost finding today, it is a placement one: the first asker is `Moments(variable)`, and only the actor's events know whether anything is bound.
   - Beside it, `event/list/this.cs:75–76` (`Moments`) scans every event per set (`foreach … Item.Equals(variable)`), where the removed `_named` dictionary answered in one lookup. With the system events alone it is a few compares; a program that binds many events pays it on every set. Ask by name first (the actor says "something is bound on a variable named x"), make the variable only for the answer yes. Not blocking.

3. **Pattern 3 — a type-switch in the registry. Low.** `event/list/this.cs:128`, `Remove`: `@event.Item is variable.@this variable && …_on.TryRemove(variable…)`. Which item forgets its list when its last event goes is the item's choice (a virtual on `item` with the variable overriding, or the list dropping any item that has no event left, with no `is`).

4. **Pattern 12 — `is X` inside an answer. Low.** `type/item/variable/this.cs` `Names`: `if (at.Peek() is @this reached) return reached.Equals(variable);` mid-walk; the word `Names` now answers four questions (`variable.Names(variable, context)`, `code.Variable.Names(context)`, `type.Names(string)`, `IAbout.Names(string)`). Leave the word, but the two variable ones answer "is this reference that variable" and "is this reference anything": the second is `Cover`'s check at `goal/step/this.Validate.cs:79` and could be asked of the reference (`v.Names(context)`) rather than through `v.Code.Root`.

5. **Readability / stored twice. Low.** `goal/step/this.Validate.cs` `Cover` (boy scout, the file is touched): the answer is parsed twice (line 56 and line 78, once case-insensitive, once not), and the texts are kept beside the variables they came from (`said`/`answered` beside `stepped`/`answers`). Line 66 is an assignment inside a condition (`if (named = await a.Names(v, context)) break;`); an `AnyAsync`-shaped helper reads in one line. `Cover` is about 110 lines; not this branch's size, but it was touched.

6. **Raw hand-off. Low.** `goal/pattern/this.cs:18`: `at.Combine(glob).Raw` is handed to `path.Resolve(rooted, context)` again — a path turned to text to be made a path. Take the combined path as it is. `goal/list/this.cs:358`: `seen.Add(source.Raw)` keeps the visited sources as raw strings beside `source.Equals(g.Path)` one expression later; a path set (path has its own equality) answers once.

7. **Raw-form compares left (the new tell), in files this range touches. Low.** `type/list/Registry.cs:280`: `ReferenceEquals(taken.Owner, kind.Owner) && string.Equals(taken.Name, kind.Name, …)` — `kind` answers `Equals` (`kind/this.cs:370`, by name). Everything else the sweep touched is clean: `actor/this.cs:222–232` and `event/*` and `goal/list` use the type's own `Equals` now, `test/this.cs:189` uses `path.Equals`. Untouched-file hits (`type/this.cs:302,349`, `goal/step/action/serializer/Snapshot.cs:59`) are for another branch.

8. **Readability. Low.** `goal/this.cs:241` `Reference`: a ternary inside a ternary (`(Path is { } path ? path.Equals(caller?.Path) : caller?.Path is null) ? null : Address`). Two lines with a local, or `Path.Equals` taking null. `event/call/this.cs` `Kept` is a creative name for "the Sub its record keeps" (`Sub(record, context)`).

## Closed, with thanks

- `Elsewhere` (a flag on goal that forked `Start`) is gone: `app.parent.goal` is a goal subclass that refuses nothing and sends the call up (pattern 3); `call.goal.Leave` owns the return fold, `goal.Start` asks the frame (`frame.Leave`). The fork is now an element.
- `goal.App` is a birth fact, set by the reader from where the `.pr` lies (pattern 5), not searched at call time. Held memory is flat, so the goal's app link does not keep a disposed sub app alive.
- `Sub`/`IsInheritable`: a choice on the event (pattern 3), saved and read back (`SavedBindingsTests` pins both).
- The load that fails is let go (all or nothing, the next ask loads again).
- `type/list` keeps a class's type per list with a version of the lists under it: correct invalidation (the counters only grow), and a sub app's cache is its own layer (nothing written to the shared list). It costs a walk over `Under` per lookup; fine.

## Not done

No mutations; the new `Interlocked.Increment`s in `Registry.cs` and the `_classes` cache were read, not deletion-tested. Probes of the set (3.5 KB) and parse (720 B) figures were not re-run: the request rows are the net.

## Verdict v4: **FAIL (narrow)** — coder next, then back to me for a read of finding 1. Everything else is low and can go with it. If the architect rules the five direct `goal.list.Find(name, caller)` sites stay by design and the two runtime sites share one ask, this is a PASS.

---

# Recheck v5 — 86d90db97..100305adb (88)

1. **Closed.** `goal.Find` is gone. `goal.list.Find(name, caller)` redirects to the caller goal's own app when it is another app's (`goal/list/this.cs:205`, `caller?.App is { } home && home != App`; the recursion ends because the caller's app is then the list's own). `goal.Select` and `event.call.Start` call the list like the other five sites: one door, no ternary at either runtime site. A caller of no app (the os's, one made outside a list) is found here, as the doc comment says.
3. **Closed.** `event/list` `Remove` has no type test: a list the removed event was in (`@event.Item.Has(item)`) is dropped when no remaining event is bound on its item, and made again on its next ask. Low note: that also drops a goal's list while "each goal" events still apply to it; it is rebuilt from what applies on the next ask, so the answer is right, but a goal already running holds the old `on` for its `after` and would not see an event bound mid-run. Not blocking.
4–8, closed: `Names` walks every hop and asks `Equals` (no `is`), `Cover` keeps `stepped`/`answers` once (parsed once, `Distinct()` on the variable's own equality, `IsProgram` for the `!` test, a local `Named`), `pattern` makes one path and takes `Name`/glob from `Path.Raw`, `Registry.cs:280` asks `taken.Equals(kind)`, `Reference` is two guards, `Kept` is `Sub`.
Low notes: `Names` now answers true for a variable reached at any hop, where it stopped at the first one reached; and `at.Peek().Equals(variable)` has no null guard (an initialized hop answering null would throw). Neither is reached by a test I know of.
2 is kept as ruled by the architect.

## Verdict v5: **PASS** — tester next, then security (stage 8), auditor, docs. No bench for this diff (neither change is on a birth or request allocation path); the 86d90db97 row stands.
