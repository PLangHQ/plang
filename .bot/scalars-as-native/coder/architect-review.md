# Coder review — `scalars-as-native` plan

Read: `plan.md`, `handoff.md`, `plan/test-strategy.md`, `plan/test-coverage.md`, all seven `stage-*.md`.
Claims grounded against the current `collections-are-data` tree (the branch was authored from it).

**Verdict:** strong plan. The vertical per-type cut, "compiler is the census," and the structural
kill of the double-wrap footgun are all sound. Comments below are about one decision whose blast
radius is wider than the writeup admits — #1 and #2 are worth settling **before Stage 1 opens**,
since `item`'s member shape is the foundation every wrapper inherits.

---

## 1. `where T : item` touches far more than scalars — and some of it isn't a "value"

The scope table and Stage 7 enumerate scalars + `Variable` + `dict`/`list`. But the constraint sits
on `data.@this<T>`, so the compiler flags **every** `Data<T>` instantiation. Verified census on this
branch:

```
Data<Ask>      (3)   Data<snapshot> (1)   Data<path> (1)
Data<object>   (4)   Data<string/number/bool> (leaf swaps, expected)
Data<T>        (24)  Data<U> (1)          ← generic pass-throughs
```

Two facts the plan doesn't name:

- **`path`/`image`/`code`/`Ask`/`snapshot` are not `: item` today** (verified — each implements
  scattered interfaces, no common base). For the constraint to compile, *all* of them must inherit
  `item`. Consistent with the thesis (anything riding a `Data` slot is a value), but `snapshot` is an
  execution-state capture, not a scalar — making it `is-a item` is a real call, not a mechanical
  swap. Put these five in the scope table so Stage 7 isn't a surprise.

- **The constraint cascades through the generic layer.** `Data<T>` (24×) and `Data<U>` (1×) are
  `Merge`/`Clone`/`Ok<T>`/`Fail<T>`-adjacent infrastructure. Each generic method/class that names
  `Data<T>` must also carry `where T : item`. "Fix until green" is accurate, but *this* is the real
  cost — the leaf-signature swaps are the easy part.

## 2. `item`'s contract should be virtual-with-defaults, not pure-abstract

Follows from #1; the sharpest design point. The plan frames equality/order/truthiness as **abstract**
members every wrapper overrides — perfect for the seven scalars. But `Variable`, `Ask`, `snapshot`
(and arguably `path`) are reference-ish `item`s where value-equality is reference identity, order is
meaningless, truthiness is "present?". If the contract is abstract, each ships a throwing stub — a
latent runtime `NotSupported` the instant resolution is skipped and one slips into compare/`if`.

Recommendation: make `AreEqual`/`Order`/truthiness **virtual with reference-sane defaults**
(reference equality, incomparable/unordered, truthy-if-non-null). Scalars override; the reference-ish
`item`s inherit defaults and never write a stub. The plan already concedes this for `Variable`
("members can be minimal") — generalize it instead of treating `Variable` as the lone exception. The
"one base vs N interfaces" argument is untouched; this only flips abstract→virtual on those three.

## 3. Mid-migration `string` ↔ `text.@this` is a dict-key hazard

Same failure class as the O1 dict-aliasing finding on `collections-are-data`. The implicit
`text.@this ↔ string` operator makes a missed site *compile and run* — but it does **not** make it
*hash-equal*: a dict keyed with a raw `string` in a not-yet-swept window won't match a `text.@this`
lookup (different `GetHashCode`). The per-type vertical cut shrinks the window to within Stage 2, but
a dict with mixed-provenance keys mid-stage is a silent miss. Add an explicit regression test
(raw-string-keyed dict, `text` lookup) rather than trusting the implicit operator to cover it.

## 4. Smaller notes

- **Stop deferring dict/list-as-`item`.** Stage 1 says "do it here if free, or any stage before 7."
  It *is* free (both already implement the three interfaces). Pin it to Stage 1 so the constraint
  story is clean from day one — don't leave it floating.
- **`Data.ToBoolean()` sync vs `IBooleanResolvable` async.** The condition pipeline went async for
  `path` (I/O truthiness). Scalar truthiness is all sync — confirm the plan keeps the sync
  `ToBoolean()` path genuinely reachable for scalars so hot `if %bool%` doesn't eat an async hop just
  because the value is `IBooleanResolvable`.
- **The double-wrap kill deserves first-class billing.** `@this<T> : @this` and base `Data` is not an
  `item`, so a `Data<item>` slot structurally cannot nest a `Data`. That's the strongest single
  payoff in the branch; the plan footnotes it. Promote it to an explicit Stage-7 acceptance
  criterion.

---

*Filed by coder for architect hand-off. #1 + #2 gate Stage 1.*
