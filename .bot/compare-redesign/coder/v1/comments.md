# Coder comments on the comparison-redesign plan

Read `plan.md`, all six stage files, and both test docs; grounded every load-bearing
claim against the real code on `compare-redesign`. This is peer feedback for the
architect to read before coder picks up Stage 1 — not a blocker. Verdict first, then
the concerns ranked by how much they'd change the implementation.

## Verdict

The spine is right and I'd build it. The premise checks out against the code: today
`app.data.Compare.Order` returns a raw `int` and throws `NotOrderableException`
(`PLang/app/data/Compare.cs:34`), and `Operator.NormalizeTypes` is symmetric
(`Operator.cs:178`) — exactly the sign-bearing, coercion-in-a-separate-pass shape the
redesign removes. The four invariants you marked non-negotiable (value lives once raw,
one lazy `ValueTask` door, rank-owned-by-the-type for antisymmetry, sync ordering math
with I/O hoisted) are the correct load-bearing set, and the `Comparison` enum +
`NotEqual`/`Incomparable` split is internally consistent with the boundary table.

What follows is where the plan is thinner than the code it has to land on.

## 1. The lazy async I/O source is net-new, not a wiring change — and it's the real Stage 2 (biggest gap)

`plan.md` and `stage-2` present the door as:

```csharp
_value = await _source.ReadAndParse();   // the lazy I/O
```

There is **no `_source` and no async read path today.** The lazy machinery that exists is:
- `_valueFactory` — a **synchronous** `Func<object?>` (`this.cs:28`, one caller),
- `_raw` + a **synchronous** `Materialize()` (`this.cs:218`, `MaterializeCount` probe at `:294`),
- `ILoadable.LoadAsync()` — a **per-type** interface for image/binary reference
  fundamentals (`PLang/app/data/ILoadable.cs`), *not* a Data-level source.

So "lazy I/O is real" is not "flip the door to async." It's: design the async source
abstraction that `_valueFactory`/`_raw`/`Materialize` collapse into, decide how `ILoadable`
folds in (or stays separate), and migrate the read rung onto it. That's a substantial
piece of net-new design hiding inside one bullet of Stage 2. **Recommend splitting it
out** — either an explicit Stage 2a ("the async source: replace `_valueFactory` + `_raw`
+ sync `Materialize` with the source abstraction") ahead of the door, or at minimum a
stage-2 section that names the shape. As written, the door's `await _source.ReadAndParse()`
reads as if the source exists; it doesn't, and that's the largest single chunk of work
on the branch.

## 2. The "~990 `.Value` reads" figure is a raw grep that overcounts, and mechanical migration is unsafe

`grep -rn '\.Value\b' PLang --include=*.cs` is exactly 990 — but `.Value` is heavily
overloaded:
- ~74 are obviously not `Data.Value`: `Lazy<T>.Value` (`app/this.cs:178`),
  `KeyValuePair.Value` (`Properties.cs:53`), `Nullable<T>.Value`, `JsonElement`.
- The **type-wrapper views own a `.Value` accessor too** — `text.@this.Value`,
  `number.@this.Value`, `choice.Value`, `TString.Value`. Those must **not** become
  `await data.Value()`; they're a different receiver. Post-flip they're the very thing
  being redesigned, and the grep can't tell `data.Value` from `textView.Value`.

So a find-replace across 990 sites will rewrite the wrong receivers. The migration has to
disambiguate by receiver type, which means it's not mechanical — it's 900-ish individual
judgements. Two asks: (a) re-scope the estimate as "sites needing per-receiver
type-checking," not "mostly mechanical convert"; (b) **decide explicitly whether the
views keep a sync `.Value`** after the flip. If `text.@this` still exposes `.Value`,
half the grep hits are legitimately left alone and the door rule only covers `Data`
receivers — that decision needs to be in Stage 2 or the migration has no stopping rule.

## 3. Stages 2–4 are not independently green-able — say so as a hard statement

The plan calls Stage 2 "coupled with 3–4" and the coexistence note suggests keeping
`IEquatableValue`/`IOrderableValue` alive so the old mediator still dispatches. But
flipping the value slot to raw CLR (Stage 2) while `app.data.Compare` + `ScalarComparer`
still run (until Stage 6) means the *old* compare path is what has to stay green at
Stage 2's exit, over a value shape it wasn't written for. Realistically Stages 2→4 land
as **one commit, green only at the end of 4** — the per-stage push gate ("green both
suites at every stage") can't hold for 2 and 3 in isolation. Better to state that
outright than to discover it mid-stage and improvise. The narration into stages is still
useful for review; the green gate just moves to the 2–4 boundary.

## 4. The throw-on-`GetHashCode`/`Equals` tripwire collides with live keying — sequence it per-type with the raw flip

`GetHashCode`/`Equals` on the views are **used today**: `TString.GetHashCode/Equals`
(`TString.cs:104,109`), `choice` equality (`choice/this.cs:64`), and these back dict/set
keying. The plan's claim "collections key on the raw materialised value, not the wrapper"
is true *only after* the flip lands for that type. So the throw and the raw-value-keying
flip must ship **together per type** — if the throw precedes the flip for any type, dict
keys explode mid-migration. This is a different axis from the `IEquatableValue` coexistence
note (that's about the *mediator*; this is about *CLR collection keying*). Worth one
explicit line in Stage 2: "GetHashCode/Equals throw only once that type's value-slot is raw."

## 5. `contains`/`in` on an `Incomparable` element pair — error or "not found"? (Stage 5 decision)

The boundary table says **every** operator errors on `Incomparable`. Stage 5 has
`contains`/`indexof`/`unique` key on `Equal`. So `[%dict%] contains %number%` — a list of
dicts asked whether it contains a number — produces `Incomparable` per element and, by the
table, **errors** rather than returning `false`. That may be intended (type mismatch is a
bug), but a `contains` that throws on a type-mismatched needle instead of answering "no"
is a surprising developer surface, and `unique` over a mixed list would error rather than
keep distinct elements. Pin this explicitly in Stage 5: does element-wise `Equal`-keying
treat `Incomparable` as `false` (not found / distinct) or as an error? The table currently
forces "error" and I don't think that's what you want for membership.

## 6. `Value` is `virtual` and overridden — the door loses the override seam

`Value` is a `virtual` property with real overrides: `this.cs:1566`
(`public override object? Value => _valueFactory()`) and behavior overrides on subtypes.
Turning the property into a `Value()` method removes the polymorphic seam those overrides
use. Not a blocker, but the plan should say how a subclass participates in the door now —
override `Load()`? a protected source hook? Right now "remove the public `.Value` property"
silently drops an override point that at least one subclass depends on.

## Smaller notes

- **`Peek()` rename** — agree, and "unparsed-vs-parsed" is the right axis. `ScalarValue`'s
  current body (`this.cs:247`) already does exactly the no-parse read, so this is a clean rename.
- **`Diff` rename** — clean; confirmed no production callers, ~14 test sites in
  `DataCompareTests.cs`. Fine as the last step.
- **Default-compare-must-stay-sync vs the async drift in `path`** — Stage 5 leans on
  `path` ordering by name being sync. Note that `path` truthiness just went **async**
  (`IBooleanResolvable`/`ExistsAsync`, per the project rules). Keep the "default compare is
  sync" invariant explicit on `path` specifically, so nobody later "improves" `path`'s
  default order into existence-based and quietly forces phase-2 to block.
- **`ValueTask` await-once across ~900 sites** — the discipline ("await once, never
  `.Result`") is correct but easy to violate at that scale in a loop or a LINQ projection.
  Worth a Roslyn analyzer or at least a grep gate in the stage, not just prose.

## Bottom line

Build it. The two things that would most change the implementation are **#1** (the async
source is a stage-sized piece presented as a bullet) and **#2/#3** (the 990 figure and the
non-mechanical, not-independently-green nature of 2–4). Everything else is a clarification
to pin before the relevant stage, not a redesign.
