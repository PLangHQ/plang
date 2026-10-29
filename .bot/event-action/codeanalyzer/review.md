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
