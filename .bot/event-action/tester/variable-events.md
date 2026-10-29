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
3. **By name, loaded on the name's first set or remove (recommended; rewritten 2026-10-29 to the lazy form, pattern 11 — the first version of this option placed the event when it was bound, which is eager placement; decision 34 and stage 2 already say the lazy form).** Binding stores an event on a named variable (the debug watch, `after %count.on.set%`, a future `on.event` on a variable) **in the actor's register under its name**, where any bound event is kept, and makes no list. The name's run list is **loaded by its first set or remove**, whoever asks, taking every event that applies (the type's, and those bound by that name); a later bind joins only the names already loaded. A set runs **one** list: the name's when the name has one (it already holds the type's events, in the order bound, because the variable type answers yes for every variable), else the type's (one string lookup, case-insensitive). Running both would run a type event twice and lose the order between a type event and a name event. Nothing bound at either → today's cheap path, unchanged; nothing is asked of any object. The variable object is made **only when something fires**, and the name is the identity the watch filtered on (so the `Is` closures and `Scope.actor` disappear from the watch: the event holds `"count"`, not a predicate). **Held memory stays bounded only if a name with no by-name event keeps no list of its own** (its first ask finds nothing in the register under its name and runs the type's list as it stands); otherwise a type-level event would load a list for every name ever set, which is the growth of option 4. One answer (pattern 12): one member, `On(name)`, answers "which list runs for this name": the name's, else the variable type's, never null; the caller holds no `??`.
4. (rejected) A list per variable object made at its first set: 1.46 KB per object, never freed; it is the growth above.

## My suggestion

**Option 3 in its lazy form, plus make `Named` cheap for the fires that remain.** The measured bytes hold: they come from the prototype's cached variable object and from the miss row, neither of which depends on when the event is placed.

- Binding stores the event in the actor's register under its name (no list made). The type-level events are in the type's list as the plan has them.
- A name's list loads on its first set or remove; later binds join only names already loaded. A name with no by-name event keeps no list (held memory grows with names that have events, never with sets or all names set).
- A fire makes the variable (one parse) and hands it to the event; the name filters, not a predicate. 28.7 KB for a bare `%count%` is far more than the work needs (a name and a one-hop code); the parser is the one definition of a reference, so the fix is to **have the parser answer a bare root name cheaply**, not to bypass it. I did not profile the parser; the 28.7 KB is the figure to beat, and anything under ~1 KB makes a type-level event on every set affordable. The loaded name's entry may hold its one variable object (bounded by names that have events).
- No per-name cache for type-level fires (it would grow with names).
- Remove: the same door; the name's loaded list goes when its last event is unbound (the plan's boy-scout rule: "unbinding the last event leaves no entry").

Proof tests for the coder (each red without its change). Pattern 11, lazy: (1) before any set of `count`, binding an event on `count` makes no list for it (bytes and entries unchanged); (2) the first set of `count` loads its list from every caller kind: an early set (before the first goal ran), a set from a sync-looking path (a `Restore`, a C# `Variables.Set`), and a set made inside an event of `count` itself (nested in the load), each fires the event exactly once; (3) a bind made after `count` loaded joins its list and fires on the next set; a bind for a name not yet set makes nothing. Bytes: (4) a watch on `count` leaves the bytes of a set of any other name within a few hundred bytes of the nothing-bound path (this table's `watch miss` row is 34,363 vs 5,009); (5) 10,000 sets of distinct names with an event bound on one other name hold no more memory than with none (`GC.GetTotalMemory` before/after). Pattern 12: (6) one member (`On(name)`) answers which list runs for a name; a type event and a name event on one name run once each, in bind order; a test that has to set up the type's list and the name's list separately to agree, or a caller doing `[name] ?? [variable]`, is a second answer. (7) unbinding the last event on `count` leaves no entry.

## Limits of this measure

- Through `Variables.Set`, not a built `.goal` and not `--debug` on the command line: the `set` action's own work is not in these numbers, only what the variable door adds. The watch binding is built as `setting.Watch` builds it (type-level, `Scope.actor`, name predicate).
- The fire counts are read from the code, not counted.
- Times: noisy; use the bytes.
- `Remove` shares `Before`/`Named`; I did not measure it separately.
- The plan's lists did not exist to measure; option 3's expected cost (5 KB path + a string lookup) is derived from the prototype's 6.3 KB, not measured.
