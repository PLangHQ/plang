# Coder — collections-are-data — v2

**Resolves codeanalyzer v1** (`57118474a`). Both suites green from a clean rebuild:
**C# 4082/0** (4080 + 2 new regression tests), **plang 273/273**. Build clean, 0 errors.

Addressed the blocking pair (F1, F2) and the cleanup (F3, F4). F5 was investigated and
**declined with cause** (see below). Also swept three more sites carrying the *same*
divergent-equality smell the branch exists to delete.

## F1 — text ordering now agrees with text equality (resolved)

`app/data/Compare.cs`: the `Order` text arm was `CompareOrdinal` (case-sensitive) while the
`AreEqual` text arm was `OrdinalIgnoreCase` — so `"a" == "A"` and `"a" > "A"` both held
(trichotomy violation; sort+unique drifted). Changed the `Order` arm to
`string.Compare(a, b, OrdinalIgnoreCase)` — one case policy across ordering and equality.
Doc comment corrected (`lexical/invariant` → `lexical/case-insensitive`).

## F2 — `contains` / `in` route through the one compare path (resolved)

`app/module/condition/Operator.cs`: `contains`/`in` fell through to a private
`Operator.AreEqual` that did reference equality for dict/list, diverging from `==`
(`Compare.AreEqual`, structural). Worse, a native `list.@this` isn't `IEnumerable`, so
`contains` didn't even reach that path for native lists. Fix:
- Exposed `Compare.AreEqualValues(object?, object?)` (the same structural path `AreEqual` uses).
- `Contains`/`In` now handle `list.@this` natively (iterate `Items`, compare via `AreEqualValues`).
- Deleted the private `Operator.AreEqual`.

## F3 — dead `Operator.Compare(object?, object?)` deleted (resolved)

Zero callers after ordering moved to `Compare.Order`. Removed.

## F4 — all list collections native, legacy raw-list branches deleted (resolved — *Ingi's call: make all native*)

Every list op carried a native branch **and** a divergent legacy `List<object?>` branch
(`Comparer<object>.Default`, `Distinct()`, raw `list[i]=`), reachable because `range`/`flatten`
still emitted raw lists. Unified on one native path:
- Added `list.@this.FromRaw(value, context)` — build-at-edge: an already-native list passes
  through unchanged; a raw `IEnumerable` is wrapped element-by-element as Data, nested raw
  lists converted recursively. No raw `List<object?>` survives to diverge.
- `sort`, `unique`, `reverse`, `set`, `remove`, `flatten` now call `FromRaw` and dropped their
  legacy algorithm branches. The four in-place mutators (`sort`/`reverse`/`set`/`remove`)
  **promote-and-write-back** the native list (`Context.Variable.Set`), mirroring `list.add`'s
  existing raw→native promotion — so mutation persists and the variable becomes permanently native.
- `range`, `split` emit native `list.@this` directly (the two remaining raw producers).

## Beyond the findings — same smell, swept (one compare path)

`list.@this.Remove`, the `list.contains` action, and the `list.indexof` action all used raw
`Equals(item.Value, target)` (reference equality for dict/list, type-strict for numbers).
Routed all three through `Compare.AreEqualValues` so membership/removal agree with `==`
everywhere — structural for dict/list, numeric-widening, case-insensitive text.

## F5 — declined (the suggestion would regress untyped list elements)

The reviewer suggested keying `Wire.IsDataShaped` on `type`/`signature` presence instead of
`name`+`value`. Traced the writer (`channel/serializer/json/writer.cs`): it emits `name`+`value`
on every element but `type` **only when non-null** and `signature` **only when signed**. A list
of plain values (`[1,2,3]`) serializes each element as `{name:"", value:1}` — no `type`, no
`signature`. Keying the discriminator on those slots would fail to lift exactly those elements
back to Data, a regression. The `name`+`value` heuristic is correct for this writer-controlled
wire (list elements are never user objects — the writer always envelopes them). Left as-is;
the existing comment already records why.

## Tests

- New: `Stage4_TypedCompareTests.Compare_TextCaseInsensitive_OrderAndEqualsAgree` (F1 trichotomy),
  `Contains_StructuralDictEquality_MatchesEqualsPath` (F2 structural contains/in).
- Updated 8 list-op tests to assert the native `list.@this` result shape (the ops now return
  native lists end to end, matching how the runtime stores collections).

Hand back to **codeanalyzer** for re-review.
