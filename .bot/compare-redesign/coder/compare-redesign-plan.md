# Comparison redesign — execution plan

A value's comparison is owned by its **type**, operates on **Data** (never decomposed except at
the leaf), is **async**, and dispatches **polymorphically** (no `Type.Name` switch). This plan is
the source of truth for the build; follow it step by step. **Re-read it before each step.**

> Base: this branch (`compare-redesign`) is cut from `origin/scalars-as-native` (`cbbe00a3d`) —
> the green base with the prerequisite work (literal-judgement, the `error.handle` fresh-build fix,
> the foreach/`GoalCall` fix, json-null + temporal coercion, the ScalarsAsNative acceptance suite).
> **Push after every step** — the container crashed mid-session once and wiped local-only commits;
> never rely on unpushed work.

## The rules (settled with Ingi — do not violate)

1. **Data = raw value + Type tag.** `Data.Value` is the raw value (`5`, `"hello"`), NEVER a type
   wrapper. The `Type` is metadata about the raw value (derived from its CLR type or set explicitly).
2. **A type never holds the value — it references the Data.** `text(data)`, not `text(string)`.
   Holding a copied-out scalar breaks OBP ("pass the object, don't decompose"; smell #6 — a flat
   copy of what's reachable through the reference). The type reads the value *through* its Data.
3. **`a.Compare(b)` is the surface, on Data.** Not `a.Type.Compare(b)` (reads as "compare the
   types"). Data forwards to its type's behavior; the type does the work.
4. **Each type owns BOTH coerce + compare.** `date.compare(other)` coerces `other` into a date
   (it already is one, or parse it from text) and compares dates. Per-type — there is NO single
   generic ordering leaf shared across types (no per-type `DateOnly.CompareTo`/`string.Compare`
   scattered AND no one generic `Sign` either; the type owns its coerce-and-compare).
5. **Decompose only at the leaf.** Data threads through the whole chain; the one place a value is
   read is where the primitive compare actually happens. Never hand a method a pre-decomposed
   value — it's `Parse(data)` / `compare(data)`, never `Parse(string)` / `compare(rawValue)`.
6. **Coerce through the type system, not raw `ToString`.** Render the other via its Type
   (`other.As("text")` → `bool`→`"true"`, `date`→ISO). A raw CLR `ToString` is wrong (raw
   `bool`→`"True"`, raw `DateOnly`→a culture string).
7. **No `ScalarValue`.** One value, materialized on need. Delete `ScalarValue` (22 production
   sites + C# test fixtures).
8. **Materialize is async (file read is I/O) ⇒ `Compare` is async.** `Task<Comparison>`. Everything
   in PLang is async — **including sort** (no `List<T>.Sort` sync comparator; materialize keys,
   async order).
9. **`Comparison` enum** `{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign-bearing
   numbers (a magic `NotEqual = -2` would satisfy `< 0` and corrupt sort). `==` is `Compare == Equal`;
   `<`/`>` read `Less`/`Greater`; `NotEqual`/`Incomparable` → the **boundary** (operator/sort/assert)
   returns a PLang error — the value never throws.
10. **nulls last** in ordering. Condition `<`/`>` treat a null operand as false (BothPresent) —
    distinct from sort's nulls-last.

## Open design point to settle IN step 4 (not before)

Polymorphic dispatch Data→type-behavior with no `Type.Name` switch: the type entity
(`app.type.@this`, which `Data.Type` returns) must hand back its compare behavior constructed over
the Data — e.g. `this.Type.Behavior(this)` returning the `text`/`number`/… object referencing
`this`. Decide whether that's a virtual on the type entity, a small per-name registry, or the
existing reader/conversion dispatch reused. Resolve when we reach step 4; do NOT hardcode a name
switch as the final shape.

## Execution steps

- **0. Reset to green.** Be on the green base (this branch, cut from `scalars-as-native`, 307/307).
  Confirm C# + PLang green from clean.
- **1. Foundation.** Delete `ScalarValue`; route all sites to `Value`. (Async value-resolve is
  deferred to step 3-4, where Compare goes async — a no-op stub until async file-read
  materialization lands.)
- **2. `Comparison` enum** (add; per rule 9).
- **3. Per-type async `Compare(data other)` over Data references.** Each type references its Data,
  coerces the other via the type system, compares at the leaf, returns `Comparison`. Order:
  `text` → `number` → `bool` → `null` → `date` → `time` → `datetime` → `duration` → `binary` →
  `dict` → `list`. Prove `text` end-to-end before replicating.
- **4. `Data.Compare(other)` async, polymorphic** (resolve the dispatch design point above). No
  `Type.Name == "..."` anywhere.
- **5. Consumers → async.** Condition operators (already async — wire `Cmp`/`OrderOf` to async
  `Compare`), `assert` (Equals/NotEquals/Greater/Less/Contains async), `sort` (async — materialize
  keys, async order; no `List.Sort`), list ops (`contains`/`indexof`/`unique`/`in`).
- **6. Delete the old mediator.** `app.data.Compare` static, `ScalarComparer`, `NormalizeTypes`,
  `IEquatableValue`, `IOrderableValue`, and every old per-type `AreEqual`/`Order`.
- **7. Green both suites.** C# + PLang. Triage residual; the original-7 semantic reds
  (type-entity↔name, callback wrapping, afterCount-is-a-dict, hash value-equality) get resolved or
  flagged as real bugs.

## Done

Both suites green on fresh build; no `ScalarValue`; no CLR-guess wrapping; no `Type.Name` switch
in the compare path; no static compare mediator; every scalar type references its Data.

## Decision log

- **Step 1, ScalarValue removal → option (a):** output materializes too. All reads use `Value`
  (materialize on need); lazy still holds for the never-touched case (perf), verified by the type
  stamp staying `item/json` / `table/csv` (a parse would re-stamp `dict` / expose rows), not by
  output/compare staying raw. The two `LazyDeserialize` goal tests that asserted "untouched stays
  the raw string" via compare are rewritten to assert laziness via that type stamp.

## Pitfalls from the prior (lost-to-crash) attempt — avoid

- Do NOT reintroduce `Lift` (wrapping a raw scalar into a type instance by guessing from its CLR
  type — ignores the `Type` tag; the `"5"`-typed-text would be wrongly compared as number).
- Do NOT pass `other.ScalarValue` / a decomposed value INTO `Compare`/`Parse` — pass the Data.
- Do NOT leave a `Type.Name == "text"` dispatch scaffold as the final shape.
- The golden-diff method on `Data` (`this.Compare.cs`) must be renamed to `Diff` (single clean
  word) so the new value-`Compare` can own the `Compare` name without clashing.
