# How a variable's set and remove find their events (event-action decision 15)

Tester, 2026-10-29, on `event-action-tester` (from `event-action` 5727816b5). No production code kept: the one prototype is `named-cache.patch` (reverted); the measuring test is `VariableEventsMeasure.cs.txt` (not in the test tree).

Pattern first: **a birth fact, not a search** (an event bound on a name is placed when it is bound, not looked for at every set), and **a run never writes an object its app shares / held memory never grows with the run**.

## What the code does today

`Variables.Set` (`variable/list/this.cs:88`) → `Before` (:194) → `Bind` → `After` (:206). Remove (:348) is the same shape. Per set:

- `type.IsBoundAt("set", when, context)` is asked first (:195, :209). Nothing bound on the variable type, nor in the app's events for it → **no variable is made**. That path costs the set alone.
- Anything bound there (even a binding for another name) → `Named(name)` (:213) parses `%name%` into a **new variable object** every time, and the binding's own filter (`Is`, `Scope`) then decides if it runs. The debug watch is exactly that: `setting.Watch` (`debug/setting/this.cs:62`) binds on the variable type with an `Is(item)` that compares `variable.Name`. So **a watch on `count` makes every set of every name pay the parse**, and drops the object if `Is` says no.
- `Variable.Level` (`variable/this.cs:28`) has two levels: the variable type, then the variable itself. The second is a throwaway (a new object per set), so an event bound on one variable object cannot be heard by the next set. Today everything that works is the type's level. (Read from the code, not run.)
- Fires: one `After` per *changed* set (the same Data set again fires nothing, `CollectionEventsTests.TheSameDataSetAgain_FiresNothing`), one `Before` per set, also for a loop's `%item%` writes into a frame (`Bind`'s frame branch reaches the same `After`).

## What I measured

Through the real door (`Variables.Set` of the User actor's memory), 1,000 sets after a 200-set warm-up, a C# harness, not a built `.goal` (no builder, no LLM). The harness binds with the same shapes: a binding on `app.variable.type`'s `set` (any name) and a watch built like `setting.Watch` (name filter). **Bytes are the stable number; times varied 2–3x with the machine's load (the other suites were running), so read them as a ratio.**

| case | bytes / set | time / set (quiet run) |
|---|---|---|
| nothing bound, one name | 5,009 | 55 µs |
| nothing bound, 1,000 distinct names | 5,230 | 76 µs |
| `after set` bound (any name), one name | **34,964** | 277 µs |
| `after set` bound, 1,000 distinct names | 35,181 | 237 µs |
| `before set` bound, one name | 40,018 | 273 µs |
| watch `count`, sets `count` (hit) | 34,998 | 295 µs |
| watch `count`, sets other names (**miss**) | **34,363** | 257 µs |
| `Named()` alone | 28,712 | ~236 µs |

- **Binding anything on the type costs ~7x the bytes and ~4–5x the time of a set, and 28.7 KB of the 35 KB is `Named()` parsing `%count%`.** A watch's miss costs as much as its hit.
- Nothing is *held* by these sets: it is garbage, but 30 KB × every set is the gen0 churn (gen0 collections 10 → 45–50 for 1,000 sets).
- **Held memory under "a list per variable object":** a variable object is 394 B; with its `set` event and two lists made in the app's registry it is **1,463 B** (the lists are ~1.07 KB). A registry keyed by that object never lets go of it. 1,000 sets = 1.4 MB held; 3,000 requests of 10 sets = ~44 MB. That is exactly the growth decision 15 fears, confirmed.

## The options

1. **The variable runs its type's list (your first answer).** Held: nothing. A variable has no `on` of its own, so a set asks one list (the type's). Gap: the debug watch and anything "this name only" must live in that list as a predicate, and a predicate takes an object, so the 28 KB is still paid per set of every name while any watch exists (the miss row).
2. **One variable object per name, per actor** (a dictionary `name → variable` in the memory, the lists on that object). Prototype, `named-cache.patch` (a `ConcurrentDictionary` in `Named`): a bound set 34,964 → **6,281 B** (the nothing-bound path is 5,009), watch hit 34,998 → 6,313. A *new* name still pays the 28.7 KB once (the distinct-name rows stay ~29 KB). Held: 394 B per name, plus 1.07 KB per name with a list made. It grows with the names a program uses (finite in source; unbounded for names made at run time, `%{key}%`), and must be dropped on Remove. It is the "everything is by reference" shape, but it makes the memory the owner of an object per name, beside the `Data` it already holds per name.
3. **Place by name when bound (recommended).** Bind is the birth: an event bound on a named variable (the debug watch, `after %count.on.set%`, a future `on.event` on a variable) is placed into a small `name → lists` map on the actor's events, **made when it is bound** (a bind is a program step; sets are not). A set is then: the type's list (all variables), and `map[name]` (one string lookup, case-insensitive). Nothing bound at either → today's cheap path, unchanged; nothing is asked of any object. The variable object is made **only when something fires**, and the name is then the identity the watch filtered on (so the `Is` closures and `Scope.actor` disappear from the watch: the event holds `"count"`, not a predicate). Held: the map has one entry per name that has an event, so it grows with binds, never with sets or names. It is the plan's own rule (a list is kept by what stays around) with the name, not the object, as the thing that stays around; the same shape as a goal found by its name.
4. (rejected) A list per variable object made at its first set: 1.46 KB per object, never freed; it is the growth above.

## My suggestion

**Option 3, plus make `Named` cheap for the fires that remain.**

- Type-level events (`after %!app.type.variable.on.set%`) and by-name events both live in the lists the plan already describes; the by-name ones in a `variable` map on the actor's events (or `actor.event` keyed by name): found by `map[name]`, with a name filter instead of a predicate.
- A fire makes the variable (one parse) and hands it to the event. 28.7 KB for a bare `%count%` is far more than the work needs (a name and a one-hop code); the parser is the one definition of a reference, so the fix is to **have the parser answer a bare root name cheaply** (or hold the name's variable in the map entry made at bind time: it is a birth fact there, bounded by binds), not to bypass the parser. I did not profile the parser; the 28.7 KB is the figure to beat, and anything under ~1 KB makes a type-level event on every set affordable.
- No per-name cache for type-level fires (it would grow with names); the map entry holds the one variable object for the names that have events.
- Remove: the same, `map[name]` on the remove side; the entry is dropped when the last event for that name is unbound (the boy-scout rule already in the plan: "unbinding the last event leaves no entry").

Proof tests for the coder (each red without its change): (1) a watch on `count` leaves the bytes of a set of any other name within a few hundred bytes of the nothing-bound path (this table's `watch miss` row is 34,363 vs 5,009); (2) 10,000 sets of distinct names with an event bound hold no more memory than with none (`GC.GetTotalMemory` before/after); (3) an event bound on `count` fires for `count`'s next set (today impossible on the variable level); (4) unbinding the last event on `count` removes the map entry.

## Limits of this measure

- Through `Variables.Set`, not a built `.goal` and not `--debug` on the command line: the `set` action's own work is not in these numbers, only what the variable door adds. The watch binding is built as `setting.Watch` builds it (type-level, `Scope.actor`, name predicate).
- The fire counts are read from the code, not counted.
- Times: noisy; use the bytes.
- `Remove` shares `Before`/`Named`; I did not measure it separately.
- The plan's lists did not exist to measure; option 3's expected cost (5 KB path + a string lookup) is derived from the prototype's 6.3 KB, not measured.
