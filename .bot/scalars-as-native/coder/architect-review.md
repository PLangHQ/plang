# Coder review — `scalars-as-native` plan

Read: `plan.md`, `handoff.md`, `plan/test-strategy.md`, `plan/test-coverage.md`, all seven `stage-*.md`.
Re-grounded against the rebased branch base, which now carries `Documentation/v0.2/type-system.md`
(the collection model `collections-are-data` just landed) — that doc changes the review; see #1.

**Verdict:** strong plan. Vertical per-type cut, "compiler is the census," and the structural kill of
the double-wrap footgun are all sound. But the freshly-landed collection model contradicts Stage 1's
`item` shape (#1), and the `where T : item` blast radius reaches non-value domain types (#2). Both gate
Stage 1, since they fix `item`'s member surface before anything inherits it.

---

## 1. `item` must NOT implement `IOrderableValue` — it contradicts the collection model just landed

Stage 1 says: *"`IOrderableValue` / `IEquatableValue` (the interfaces `Compare.cs` dispatches on) —
implemented on `item`, backed by abstract `Order`/`AreEqual`."* And Stage 1 green requires `dict`/`list`
to become `: item`.

But `type-system.md` (now in the base) documents the opposite for `dict`:

> `dict` … **does not implement `IOrderableValue`** — dict is equality-only; `Compare.Order` throws
> [`NotOrderableException`] for it.

If `item` implements `IOrderableValue` and `dict : item`, then `dict` *inherits* `IOrderableValue` — and
the clean `Compare.NotOrderableException` gate either silently disappears (dict now claims an order it
doesn't have → wrong sort results) or `dict` overrides `Order` to throw, which is exactly the
throwing-stub smell, now buried *inside* a dispatched method instead of at the gate. Either way it
regresses a contract `collections-are-data` just audited.

The established model is **per-interface opt-in**, not one base implementing all three: `list` implements
all three, `dict` implements `IEquatableValue` + `IBooleanResolvable` but **not** `IOrderableValue`.
`bool`, `null`, `Variable` (and the `Ask`/`snapshot` of #2) have no natural total order either.

**Recommendation:** `item` is a **thin slot-fit base** — its job is to make `Data<T> where T : item`
mean something and to kill the double-wrap footgun. It should **not** be the behavior hub. Leave
`IOrderableValue` / `IEquatableValue` / `IBooleanResolvable` as the opt-in interfaces they are today;
each type implements exactly the ones it can honor, and `Compare`'s existing interface-dispatch already
routes correctly. The constraint and the footgun-kill both still land — they only need the base to
*exist*, not to carry the contract. If `item` carries anything, restrict it to the genuinely universal
pair (equality + truthiness) as **virtual-with-defaults**, and keep ordering off it entirely.

This supersedes the abstract-vs-virtual framing in the first draft of this note: the real issue isn't
abstract vs virtual, it's that ordering isn't universal and the base shouldn't pretend it is.

## 2. `where T : item` reaches non-value domain types — name them and route them to bare `Data`

The constraint sits on `data.@this<T>`, so the compiler flags **every** `Data<T>`. Verified on the base:

```
Data<Ask> (3)   Data<snapshot> (1)   Data<path> (1)   Data<object> (4)
Data<T> (24)    Data<U> (1)          ← generic pass-throughs, each needs the constraint cascaded
```

`Ask` and `snapshot` are **not** `[PlangType]` catalog values — `Ask` is the resume-sentinel
(`IExitsGoal`, `type-system.md` §1), `snapshot` is an execution-state capture under `app/snapshot/`.
Neither lives under `app/type/`, neither belongs in the value-type lattice. Forcing them `: item` to
satisfy the constraint would be wrong. Stage 7 only addresses the *polymorphic* slots (`Data<object>` →
`Data<item>`); it's silent on these concretely-typed non-value slots.

**Recommendation:** add an explicit Stage-7 rule — concretely-typed non-value `Data<T>` (`Data<Ask>`,
`Data<snapshot>`) repoint to **bare `Data`**, not `: item` inheritance. And name the cascade: the ~25
generic `Data<T>`/`Data<U>` infrastructure methods (`Merge`/`Clone`/`Ok`/`Fail`) each need
`where T : item` threaded through — that's the real cost of turning the constraint on, not the leaf swaps.
`path`/`image`/`code` (real catalog values) *do* become `: item`; that part is mechanical.

## 3. Mid-migration value-equality of wrapped scalars (HashSet / list-element / dedup)

Same failure class as the `collections-are-data` O1 aliasing finding. The implicit `text.@this ↔ string`
operator makes a missed site *compile and run* but does **not** make a raw `string` and a `text.@this`
*value-equal* / hash-equal. `list` compares elements via `IEquatableValue` element-by-element, and dedup/
`HashSet` membership key on `GetHashCode` — so a `list` or set built across a not-yet-swept window can
silently miss-match a wrapped value against a raw one. (Note: `dict` *keys* are strings via the
case-insensitive `_index`, so dict-key lookup is less exposed than I first wrote — the hazard is
element/value equality, not keys.) The per-type vertical cut shrinks the window to within Stage 2; add a
regression test for `HashSet`/list-element equality of a wrapped value against its raw form rather than
trusting the implicit operator.

## 4. Smaller notes

- **Pin dict/list-as-`item` to Stage 1, but only after #1 is resolved.** Stage 1 leaves it floating
  ("here if free, or any stage before 7"). It's mechanical *once* the base is a thin marker (#1); doing
  it before #1 is settled is what surfaces the `IOrderableValue` contradiction.
- **`Data.ToBoolean()` sync vs `IBooleanResolvable` async.** The condition pipeline went async for
  `path` (I/O truthiness). Scalar truthiness is sync — confirm the sync `ToBoolean()` path stays
  reachable for scalars so hot `if %bool%` doesn't eat an async hop just for being `IBooleanResolvable`.
- **Promote the double-wrap kill to a first-class acceptance criterion.** `@this<T> : @this` and base
  `Data` is not an `item`, so a `Data<item>` slot structurally cannot nest a `Data`. Strongest single
  payoff in the branch; the plan footnotes it.

---

*Filed by coder for architect hand-off. #1 + #2 gate Stage 1. #1 revised after the rebased base surfaced
`type-system.md`'s collection model.*
