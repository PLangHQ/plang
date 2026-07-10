# Decision — no gate: containers stop deep-rendering; `Value()` = "become your real shape" (C)

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-compare-materialization-gate.md`. Your flag-was-obpv instinct was right, and so was surfacing instead of forcing — but the answer isn't a better gate: it's that **the gate shouldn't exist**.

## The fact that picks the answer

**A typed `Order` cannot dispatch before materialization — the typed item doesn't exist yet.** Pre-render, `%jsonFile%`'s item IS a `source`, not a list; there is no `bool.Order` to reach until the value has become a bool. (A)'s `ComparableForm` and (B)'s Data-level dispatch both compensate for comparing things that haven't become themselves. The compensation points at the real disease:

**`list.Value()` deep-rendering all N elements is a relic whose consumer we already killed.** The deep render served serialization — and the Load-removal ruling moved element materialization into the one door (`container loops only await element.Output(...)`). The value model says render-never-cache, everything lazy; the old `set`-path comments name deep-render as the self-reference infinite-loop footgun. Its main caller is gone; compare just exposed the corpse.

## The ruling — (C): two deletions, zero new members

```csharp
// list/this.cs — TODAY: Value() deep-renders every element (list/this.cs:589). DELETED.
// dict/this.cs — same deletion.
// The base answers:  item.Value(data) => this   — "I am already my real shape."
// Elements are Datas; they render at THEIR doors when touched (output loop, compare walk, navigation).
// Sources untouched: source.Value still parses raw → the real shape; file/url still load.
// "Value = become your real shape" was always their contract — containers now honor it too.
```

```csharp
// data.Compare — the uniform door. NO gate, no flag, no ComparableForm, no Data-level fork:
public async ValueTask<Comparison> Compare(@this other)
    => (await Value()).Compare(await other.Value());
    // scalar/template → renders (one leaf) · source → parses to its real shape · container → self, element-lazy
```

**Two earlier compensations die with the gate:**
1. The materialization gate itself — never built.
2. **The rank-off-the-type-axis rule (the async answer's flag #1) — DELETED.** After the shallow `Value()`, both operands ARE their real types, so `item.Rank` is simply correct. One rule again; the deferred-operand driver-pick test now asserts the simpler truth (a parsed `%jsonFile%` ranks as its real shape).

`list.Order` is unchanged from the async ruling — the laziness lives in its element walk (`await Items[i].Compare(lb.Items[i])`, first mismatch exits, the tail never materializes).

## Rejected

- **(A)** `ComparableForm(data)` — a new virtual whose body is the item commanding its own courier; a shim for the pre-materialization dispatch that physics forbids anyway.
- **(B)** comparison as a Data behavior — rebuilds `CompareValues` at the Data level and still can't dispatch per-type pre-materialization.

## The gate on THIS ruling — the consumer inventory

Inventory remaining `list.Value()`/`dict.Value()` deep-render consumers. Any real one moves to the element-door pattern (as serialization did). **If a consumer genuinely cannot move, stop and surface** — that's the test of whether (C) is truly convergent, not an assumption to push through.

## Acceptance

- `%listA% == %listB%`, mismatch at element 0 of 10 000 → one element pair materialized, exit (the short-circuit proof on the real flow — carried over).
- `%jsonFile% == %x%` → the source parses to its real shape; the rank comes off the real item (replaces the type-axis test).
- The self-reference footgun pin: a list whose entries reference the list itself (`%plan.usage% = {model:%plan.Model%}`-style) survives `set`/carry without looping (deep-render's departure makes this structural).
- Suites hold baseline through the two deletions.
