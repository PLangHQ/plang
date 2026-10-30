# call-cost

Cut 2026-10-30 from runtime2 7bf093245 (event-action merged). Agreed with Ingi on 2026-10-29 and 2026-10-30 ("on the questions I agree new branch", "1 yes on 4 and 5", "agree" on the property bag, "Ok" on idea 2's shape). Measurements: `.bot/event-action/codeanalyzer/call-cost.md` (sections A–G), with the bench's `cost`, `phases`, `repeat`, `cold`, `resolve` modes. ReadyToRun is not part of this branch ("no not yet").

## Why

A plang program pays for work it never asks for, and for the same answer made again on every run. Measured on event-action:
- A request app is 441 KB: its birth 99, the goal's load 40, the goal's run 300.
- A goal run again in the same app is 230 KB, and doesn't grow.
- A value in memory reads in 0.2 µs, while a `%!x%` the memory doesn't hold costs ~41 KB and ~180 µs.
- An action's dispatch is ~12 µs and 8.4 KB before the action does anything.

Garbage collection is under 6% of the time, so bytes here mean work done for nothing, not GC.

The causes all have one of three shapes, each an OBP pattern the code breaks:
- made at birth though few ask (pattern 11, lazy);
- made again on every ask though the answer is the same (pattern 5, a birth fact, not a search);
- a lookup that builds a description of what it wants instead of going to it by reference.

The fixes are those patterns applied. None adds a second path beside a door. A fast path is a fork (idea 9, not done).

## What it should give (measured in trials, else estimated)

| | today | after |
|---|---|---|
| a sub app's birth | 101 KB, 0.48 ms | ~55 KB, ~0.14 ms (idea 10, measured) |
| a goal run again | 230 KB | ~178 KB with idea 2's number births alone (measured); less with 3, 4, 5, 12 |
| a first goal run | 300 KB | ~246 KB with idea 2 alone (measured) |
| a `%!x%` memory doesn't hold | ~41 KB | ~1 KB (idea 1, estimated; part of it went with event-action 74) |
| an action's dispatch | ~12 µs, 8.4 KB | ~7 µs (ideas 4 and 12, estimated) |
| a request app | 441 KB | ~300 KB (sum, estimated) |

The codeanalyzer measures each stage before and after on the same bench. The table's estimates are replaced by what it finds.

## Stages

1. **Made on the first ask** (pattern 11).
   - The step cache: `app/this.cs:199` `Cache { get; internal set; } = new module.cache.Memory()`, a .NET MemoryCache at every app's birth (47 KB, 29 µs). Only `on.cache` and the signing nonce ask for it. It's made on its first ask.
   - A frame's own variable store: `call/this.cs:247` `_parameter = new(stack.Properties)` in every frame (goal, step, action, isolate), ~1.2 KB each, though almost none writes a local. Made on its first write. Also the frame's `Tags` and `Errors` (−3% measured).
   - `goal.Broken()` (`goal/this.cs:395`) builds a list of the goal's broken steps at every start (`Step.Items().Where(...).ToList()`). A goal's steps don't change after its load, so it's judged once and kept (a birth fact of the loaded goal).
2. **A type is found, not made** (idea 2; patterns 5 and 7).
   - `number`'s operator birth (`type/item/number/this.cs:36–37`: `new type("number", typeof(@this)) { kind = kind }`, plus a `kind.empty` thrown away) runs ~55 times per goal run, from `list.Count` (`type/item/list/this.cs:286`), `call.Depth` (`call/this.cs:419`), `call.list.Push` and `goal.step.list.Start`.
   - The callers that have an app reach the type by reference: `App.type.list[typeof(number)]` (kept since event-action 74), then NEW `type.Kinded(kind)`, made once and kept by the type ("a kinded type made once", CLAUDE.md). No lookup key `new type("number", "int")`, no static (Ingi).
   - The operator stays for C# with no app in hand. Its callers on the hot paths move off it.
   - Then the readers that make a type per read: `type/serializer/Reader.cs:22,25,45,65` (keys `new type(...)` per read) and `type/item/bool/serializer/Reader.cs` (~3%). The same: by reference, or `Kinded`.
   - Then the items' `Type => new(typeof(@this))` sites the codeanalyzer named (find each; same fix).
3. **A lookup kept by its owner** (pattern 5).
   - Idea 1: a shortcut's `Name => Path.Stem.ToString()` (`shortcut/this.cs:25`) is made at every `Match` (5.6 KB, ~37 µs, and six shortcuts are walked per miss). The name is a birth fact, kept at construction.
   - Idea 6: `goal.list.Find` rebuilds its candidate lists per call (`goal/list/this.cs:224`, `Items().Where(...).Reverse().ToList()`, ~2.5 KB). A name → goal index per folder, made when the folder's goals are listed, kept as the list's own.
   - A frame's `Children` and `property`, still made per frame, and `Children.Add` at every push (the codeanalyzer's stage 1 note 4): made on first ask too.
   - Idea 12: the generated `Resolve` asks `context.Setting.Get(action, option)` for every parameter the step didn't give, on every run (`actor/setting/this.cs:109`; 1.5 µs and 1.0 KB each, always "not set" for a `set`). The setting store keeps its answer per address by its change count (`Set` bumps it with `Move()`), as the type list keeps a class's answer by its version (event-action 74). The owner keeps it; the action doesn't cache the setting's job.
4. **The generator** (ideas 4 and 5; Ingi's yes: a change to the locked generator shape and the action's door, re-pinned citing it).
   - Idea 4: `Instance()` makes a throwaway handler (`goal/step/action/this.cs:385`, `Module.Create`, 1.6 KB) only to call its generated `Resolve` (`:414`). Ingi chose (B), "no static": the catalog element keeps one handler of its class, made with its module's app's System context at the class's first run, and resolves through it. One birth per run, no generator change for this part. Also the unknown-property check per run (304 B) is judged once per loaded action, as `Stale` is.
   - Idea 5: the generated `Resolve` binds each lazy parameter through closures and wrappers (6.5 KB, 9.3 µs). The generator emits the typed assignment directly. Estimated half of it.
5. **A copy shares until it's written** (Ingi: "agree").
   - `data.Copy` (`data/this.cs:741`) copies the property bag at every goal-call parameter and every `set %y% = %x%`. Most variables never get a property. The copy shares its source's bag until its first write to it.
   - The codeanalyzer measures the wrapper and the bag first: if the bag isn't worth it, this stage is dropped and the log says why.

6. **HELD** (Ingi, 2026-10-30, on tables only vs a kept reflection scan: "maybe wait with tables"). The prototype showed `typeof` tables cost 39–43 ms of JIT; name-only tables resolved at first ask would save ~85–95 ms of a ~210 ms cold root. Not built until Ingi says. **The type tables made at build time, and read lazily** (Ingi, 2026-10-30: "bring it into this branch"; "and lazy ofc"; the codeanalyzer's section H and `.bot/call-cost/codeanalyzer/startup-tables.md`).
   - A cold root app is ~210 ms, and ~105 ms of it is `origin.code` scanning the PLang assembly by reflection (`type/list/Code.cs:141–181`), again for every root app.
   - The generator (a new `Emission/Library` beside Action and Property) writes each assembly's tables. They're the only door: an assembly without them brings no plang types, with no reflection scan beside them.
   - Lazy: each table is read at its first ask, and a type's entry is made when its name is first asked. Only an enumeration of every type makes them all, pinned against the old eager list.
   - First, the codeanalyzer measures a hand-copied prototype's JIT cost. Then the coder brings the shape.
   - Also one scan per process, if the tables don't already make it free.
   - The generator and the type system are locked: Ingi's yes is his "bring it into this branch". The shape still comes to me before it's built, and anything beyond it goes to him.

Every stage: the bench before and after (`Tools/Bench/run.sh`, the parts it touches), a C# byte pin that goes red without the change, C# and plang green.

## Not in this branch, on the backlog

- ReadyToRun (Ingi: "no not yet"). Stage 6 cuts startup without it.
- `app.jit` (Future).
- The six doors' repeated catches for a failed events load: decided with stage 2's measurements, not built here unless they show (a `data<on>` door allocates per ask).
- `set %x% = %x% + 1` building set-before-add: the builder bot's, before 2.0b.

## Demolition

- [ ] `Cache`'s birth at the app's construction (`app/this.cs:199`).
- [ ] A frame's store, `Tags` and `Errors` made at push.
- [ ] `Broken()`'s list at every start.
- [ ] The hot callers' use of `number`'s operator birth; every `new type(...)` built only as a lookup key on a hot path.
- [ ] `shortcut.Name`'s per-ask `Path.Stem.ToString()`.
- [ ] `goal.list.Find`'s rebuilt candidate lists.
- [ ] The per-run settings ask for an unset parameter.
- [ ] The throwaway handler in `Instance()` and the per-run unknown-property check.
- [ ] The closures in a generated `Resolve`.
- Stays: every door (no fast path beside one); `number`'s operators for C# with no app; the bench modes.

## Collision check

Read against event-action's decisions:
- 74 (the type list keeps a class's answer by its version) is what stage 2 builds on.
- 72 and Ingi's "no static": stage 2 uses no static. Stage 4's generated member is named in its shape before it's built.
- 73 (a set asks with its variable) is unchanged; its variable is cheap since 74.
- 97 (the actor's events load under the actor's lifetime) is untouched.
- Nothing here changes what an event binds or where it runs.

## Open for Ingi

None. Stage 4's shape (the generated member) is checked with him when the coder brings it.
