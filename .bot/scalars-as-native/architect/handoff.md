# Handoff — `scalars-as-native`

> **Note for coder + test-designer:** every file path, class name, signature, snippet, and test case in `plan.md`, these `stage-*.md` files, and `plan/test-*.md` is a **suggestion**. You own the implementation and the tests, and you own the final shape. The dispositions (what flows native, what collapses, what deletes) and the acceptance bars are the contract; how you get there is yours.

Read `plan.md` first — it carries the Why, the law (the two legal `is`-switch sites), the decisions (item base, `Data<T> where T : item`, bool/null/date/time wrappers), and the seam map. This file is the build order.

## The shape of the work

Every scalar value should flow as its native wrapper (`text.@this`, `number.@this`, `datetime.@this`, `duration.@this`, `bool.@this`, `null.@this`, `date.@this`, `time.@this`), each a subtype of a new abstract `item.@this`. Behavior (compare, truthiness, ops, serialize) becomes a virtual member on the type; the ~197 `is string` / `(string)value` / `value is int|bool|DateTimeOffset|TimeSpan` switches collapse into method calls or single `.Value` unwraps.

## Why the stages are cut per-type (not per-seam)

`plan.md`'s sequencing reads horizontally (build all wrappers → construction → compare → …). The stages below re-cut it **vertically, one scalar type at a time**, because that's what keeps both suites green between stages: the moment construction produces a `text.@this`, every `value is string` in handler bodies goes silently false. So each type's construction flip and its body sweep must land *together*. This mirrors `collections-are-data` (Stage 1 = dict end-to-end, Stage 3 = arrays end-to-end), not a phase-per-seam march that leaves the tree red between phases.

The central dispatchers already make this gentle: `Compare` dispatches `lv is IOrderableValue` → self before falling to `ScalarComparer`; truthiness dispatches `IBooleanResolvable`; serialization has the `Normalize` tree. So a wrapper that implements the contract self-handles the instant it exists, and the raw fallbacks shrink type-by-type rather than all at once.

## Stages

| # | Stage | Lands |
|---|---|---|
| 1 | `item.@this` base + contract | the abstract base; `number` inherits it as the reference |
| 2 | `text` native | `text.@this` built out; strings born as `text`; `is string` swept |
| 3 | `datetime` + `date` + `time` native | three wrappers; **date stops collapsing into datetime**; date/datetime/time values born native |
| 4 | `duration` native | `duration.@this` built out; `TimeSpan` values born native |
| 5 | `bool` native | `bool.@this` created; bools born native; `is bool` swept |
| 6 | `null` native | singleton `null.@this`; `Data.Null()` stamps it; `is null` value-checks swept |
| 7 | Lock + cleanup | `Variable : item`; swap remaining `Data<rawCLR>` signatures; turn on `where T : item`; `Data<object>`→`Data<item>`; collapse `ScalarComparer`; rewrite the coercion mediator; retire `Unwrap*`/`Wrap*` |

Each of stages 1–6 ends with **both suites green**. Stage 7 ends green **and** the `where T : item` constraint compiles (the compiler is the census for the signature swap).

## Before you start: the body census

Step 1 of `plan.md`'s sequencing. Grep production C# for `is string` / `(string)value` / `value is int|long|double|decimal|bool` / `is System.DateTimeOffset|TimeSpan|DateOnly|TimeOnly` (and legacy `DateTime`) / `.Value is <scalar>`, and classify each site **behavioral** (→ method on the wrapper) / **perimeter** (→ single `.Value` unwrap) / **coercion** (→ the mediator). Output the list before editing — it scopes the branch and feeds the per-type sweeps in stages 2–6. The signature swap (`Data<int>` → `Data<number>`) is **not** in this census; the constraint finds those in Stage 7.

`data/this.cs` alone has a cluster of body sites (`:144`, `:152`, `:248`, `:322`, `:346`, `:560`, …) — a good place to calibrate the classification.

## Reference shape

`number.@this` is the worked example of a complete wrapper (compare, truthiness, conversion, arithmetic). When a stage says "build out like `number`," that's the target. `text.@this` today (`type/text/this.cs`) is the thin starting point: `Value` + implicit `string` + `ToString`, nothing else.
