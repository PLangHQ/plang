# Code Analyzer — collections-are-data — v2 (re-review)

**Verdict: PASS** · **Next bot: tester**

Re-reviewed `2defdfc5f..a361d20ce` (coder v2 + architect decompose handoff + coder v3).
Clean rebuild, both suites green: **C# 4082/0** (4080 + 2 new regression tests), **plang 273/273**.
Build 0 errors. All v1 findings resolved; ToRaw decompose discipline now strictly leaf-confined.

## v1 findings — all closed

- **F1 (text == vs order case mismatch) — fixed.** `Compare.Order` text arm is now
  `string.Compare(a, b, OrdinalIgnoreCase)` (`Compare.cs:62`), matching `AreEqualValues`'
  `OrdinalIgnoreCase` (`:106`). Trichotomy restored; doc corrected. Pinned by
  `Stage4_TypedCompareTests.Compare_TextCaseInsensitive_OrderAndEqualsAgree`.
- **F2 (contains/in second equality path) — fixed.** `Contains`/`In` now handle `list.@this`
  natively and route every comparison through `Compare.AreEqualValues` (exposed public);
  the private `Operator.AreEqual` is deleted. Verified no `int Compare(`/local `AreEqual`
  remain in `Operator.cs`. Pinned by `Contains_StructuralDictEquality_MatchesEqualsPath`.
- **F3 (dead `Operator.Compare`) — removed.**
- **F4 (legacy raw-list arms) — resolved the way you called it: all-native.** New
  `list.@this.FromRaw(value, context)` is the single build-at-edge normaliser; `sort`/`unique`/
  `reverse`/`set`/`remove`/`flatten` dropped their `List<object?>`/`Comparer<object>.Default`/
  `Distinct()` branches. The in-place mutators promote-and-write-back the native list
  (`Context.Variable.Set`), mirroring `list.add` — so mutation persists and the variable
  becomes permanently native. `range`/`split` now emit native lists directly. Verified zero
  `is not List<object?>` / `Comparer<object>.Default` / `.Distinct()` left under `module/list/`.
  Bonus sweep: `list.Remove` / `list.contains` / `list.indexof` moved off raw `Equals` onto
  `Compare.AreEqualValues` — membership now agrees with `==` everywhere.
- **F5 (Wire.IsDataShaped heuristic) — declined, and I accept the decline.** The coder traced
  the writer: `type` is emitted only when non-null and `signature` only when signed, so a plain
  `[1,2,3]` serializes elements as `{name:"",value:1}`. Keying the discriminator on `type`/
  `signature` would fail to lift exactly those elements — a regression. `name`+`value` is correct
  for this writer-controlled wire. Reasoning is sound.

## ToRaw / native-type decompose audit (the lens for this branch) — clean

The architect's `coder-handoff-decompose.md` review independently flagged the same concern.
Coder v3 resolved A/B/D:

- **A — the courier-decompose is gone.** `AsCanonical`'s bind path used to run
  `IsWireShape → AsRawWireDict → dict.ToRaw()` — a recursive deep-decompose of the *whole* dict
  just to read two keys, on every container bind, flattening nested values' type-tags in the
  process. Replaced with `WireSlot`/`HasWireKey` that read a single slot via the native dict's
  `Get`/`Has` (or a raw dict) — **no `ToRaw`**. `AsRawWireDict` deleted (verified gone repo-wide).
  Behavior-preserving (same `value`+`type` discriminator); hot-path win + nested values keep their
  Data-keying. *This corrects a v1 call of mine — I had classified those two sites as "raw stays
  local, acceptable"; they were in fact decomposing more than needed on a hot path. Good catch.*
- **B — real asymmetry bug fixed.** `dict.ToRaw`'s `Unwrap` handled nested dicts but a nested
  `list.@this` (not `IEnumerable`) fell through `_ => value` and survived un-decomposed inside the
  supposedly-raw dictionary — breaking domain-record reconstruction for any record with a `List<T>`
  fed from a dict entry. Added the symmetric nested-list arm.
- **D — CommandLineParser made symmetric** (object and array both `ToRaw` now).

**Every remaining `ToRaw` call site on HEAD is a leaf or the CLI perimeter** — `catalog/Conversion`
(typed-conversion leaf), `identity`/`llm` (external-lib leaves), `CommandLineParser` (infra
perimeter), and `dict`/`list` internal recursion. The whole v2+v3 diff adds exactly two raw
collection allocations, both *inside* `ToRaw` itself. Decompose is leaf-confined; no native→raw
leaks into couriers or relays.

## One thing that needs your decision (not a code defect — escalated, not blocking)

The wire-shape discriminator still keys on **`value`+`type` presence** (`IsWireShape`,
`data/this.cs:737`). So an ordinary user dict literal like `{value: 9.99, type: "book"}` is
mis-detected as a serialized `Data` on the bind path and reconstructed as value=9.99 / type=book.
This is pre-existing, is the shared root of F5 and architect item A.45, and the coder correctly
**did not** decide it unilaterally — it's a language-semantics call (reserve a marker key, key on
`signature`, or stamp a `data` type). Already tracked: `Documentation/Runtime2/todos.md` "Fully
type-driven nested Data". Flagging here so it's on your radar; it does not block this branch's
correctness or the PASS — the collections-are-data work is sound and green.

## Verdict

**PASS** — core feature correct, all findings closed, compare path genuinely single, ToRaw
leaf-confined, both suites green. Next bot: **tester**. The `value`+`type` discriminator ambiguity
is an open design decision for Ingi, tracked separately — not a blocker.
