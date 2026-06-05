# Code Analyzer — collections-are-data — v3 (re-review)

**Verdict: NEEDS WORK (FAIL)** · **Next bot: coder** (F1 needs an architect call too)

Re-reviewed `65cd08a2d..a3cdb5f7a` — coder v4 (compare-on-value + chunk/row list model)
and v5 (`@schema:"data"` wire marker). Clean rebuild, both suites green as claimed:
**C# 4087/0**, **plang 273/273**, build 0 errors.

Two of the three changes are clean and correct. The chunk/row list model ships a
**silent cross-variable aliasing bug** that both suites miss — `add list to list`
entangles the source and target lists instead of merging. That's the blocker.

---

## F1 — BLOCKING — `add list to list` aliases the source list (it doesn't merge)

The headline of the branch is "a list is rows; `add` appends one row holding the Data
whole; reads flatten." Tracing it: `list.add` stores the element Data **by reference**
(`add.cs:32-38`), and a list row's `.Value` is the *same instance* as the added list
variable. The flattened read view and the in-place mutators (`SetAt`/`RemoveAt`/`Insert`,
via `Locate`) then descend into that shared instance. So the two lists are entangled in
**both directions** — which contradicts the stated merge semantics ("adding a list merges
its elements into the sequence", `list-rope-model.md`; "add merges", v4 report). In every
mainstream language `a.extend(b)` leaves `a` and `b` independent.

Proven deterministically (throwaway C# probe against `app.type.list.@this`, since the LLM
planner mis-builds ad-hoc goals — see note below; probe deleted, tree clean):

```
b = [50,60];  a = [10,20];  a.Add(D(b));     // "add %b% to %a%" → flat [10,20,50,60]

// write-through leak:
a.SetAt(2, 99);          → a[2]==99 (ok) BUT b[0]==99   ← mutating a corrupted b
// read-view leak:
b.Add(70);               → a.Count==5    ← a tracks a later mutation of b (expected 4)
```

The write-through case (`set item N of %a%` silently rewriting a *different* variable
`%b%`) is indefensible under any list model. The read-view case is the same root cause the
coder already noticed — it's why `Count` was made walk-on-demand (v4 report: "a row may
alias a list mutated elsewhere, so a stored counter would stale"). That note treats the
aliasing as a constraint to work around; it's actually the bug. Walking `Count` papers over
the read symptom and leaves the write-through corruption untouched.

**Why the suites stay green:** `RowModelTests.RemoveAt_FlattenedIndex_RemovesNestedLeaf`
adds an *inline* `Of(50,60)` that nothing else references, so the in-place mutation of the
shared row has no second observer to expose the leak. No test holds the added list in a
second variable.

**Fix direction (architect call):** `add`-ing a list must not leave the parent aliasing the
child. Either (a) snapshot the added list's element references into independent rows at add
time — O(k) in the *added* list's size, which still never reads the *existing* rows, so the
architect's "O(1) append, never touch existing leaves" contract holds; or (b) copy-on-write
the shared row before any mutator/read descends into it. Option (a) is simpler and matches
merge semantics directly. Whatever the choice, add a `RowModelTests` case that holds the
sub-list in a second variable and asserts independence in both directions.

## F2 — MAJOR (deferred, but a merge gate) — 2 signing tests disabled; verify round-trip regressed

`Tests/LazyDeserialize/{SignAndVerifyRoundTrip, SignedDataSurvivesInList}.test.goal` were
**active and green at my v2 PASS** (`65cd08a2d`); the `@schema` marker commit (v5) disabled
them (steps commented, inert `write out`, rebuilt). They were added on *this* branch's
lazy-deserialize stage, so this is not a regression of shipped-on-main behavior — but it is
a real, currently-broken developer flow: `sign → store/goal-call/list → verify` now fails
because the marker makes a signed Data correctly round-trip *as a Data*, and the old
`verify` path then hashes a Data-wrapping-a-Data and mismatches.

This is **honestly documented** (v5 report, commit message, `todos.md`, and a dedicated
`signature-as-schema-wrapper` branch with the fix spec) — the opposite of a silent mask, so
I'm not treating it as the gating blocker. But recording the consequence plainly: **this
branch must not merge to `main` ahead of `signature-as-schema-wrapper`**, or `verify` of any
signed value that crosses a goal/list/store boundary is broken in the product. The C# verify
probe (verify-against-raw) still passes, so the primitive itself is intact; only the
round-trip surface is affected. Tester should confirm scope and that the disable is exactly
these two goals.

## F3 — MINOR (performance regression) — materializing `Count`/`Items`/`At` inside loops → O(n²)

`Count`, `Items`, and `At(i)` went from O(1) field/index access to O(n) walks (`Items`
allocates a fresh flat `List<Data>` each call). That's fine for the EnumerateItems consumers
(they materialize once). But `list`'s own structural ops call them *inside* loops:

- `AreEqual` (`list/this.cs:292`): loop guard re-evaluates `Count` every iteration **and**
  calls `At(i)` on both sides → O(n²).
- `Order` (`:306`) and `Remove` (`:180`): `At(i)` in the loop → O(n²).
- `unique.cs:23`: `deduped.Items` re-materialized (alloc) on every outer iteration.

Correctness is unaffected and small lists are fine, but PLang lists can be large (db rows),
where `contains`/`unique`/`sort`/`==` become quadratic. Cheap fix: hoist `Items` to a local
and cache `Count` at the top of those loops.

## F4 — NIT (stale comments) — recognizer comments still describe the deleted heuristic

- `Wire.cs:422-427`: the comment block above `LiftDataIfShaped` still explains the old
  "a JSON object with both `name` and `value` keys is the canonical Data wire shape … without
  an explicit type marker" rule. The code below now keys strictly on `HasDataMarker`
  (`@schema`). The comment directly contradicts the code — update it.
- `dict/this.cs:13` references `app.type.catalog.@this` as "the list value type"; the type is
  `app.type.list.@this`. Leftover name.
- Several `@schema` recognizers say `scheme=data` in comments (`this.cs:757`, `Wire.cs:431`,
  `IsDataMarked` doc) while the constant is `WireSchema = "@schema"` ("schema", per design).
  Cosmetic, but worth a sweep so the prose matches the constant.

---

## What's clean (reviewed, no findings)

**Compare lives on the value (v4 step 1) — correct and well-shaped.** `Compare` thinned to
the mediator (null policy + `NormalizeTypes` + dispatch); `Family()` and the `Orderable`
HashSet are gone (verified no `is dict`/`is list`/`Family`/`Orderable` left in `Compare.cs`).
`dict` → `IEquatableValue` only; `list` → both, with lexicographic `Order`. `ScalarComparer`
is the single legal type-switch and now routes number equality *and* order through the number
tower (`Number.CompareTo`), killing the old order-via-tower / equality-via-`decimal.Equals`
split. The recursion contract holds (children route back through the mediator). Dispatch is
symmetric: a scalar-vs-collection compare resolves to `false` (equality) or throws
`NotOrderable` (order) from both argument orders, because the scalar fallback `a.Equals(b)`
and each collection's `other is not X` guard both reject — checked all four orderings.
`SortGuarded` correctly unwraps the `InvalidOperationException` that `List.Sort` wraps the
typed compare error in.

**`@schema:"data"` wire marker (v5) — sound, and it closes my v2 open-for-Ingi item.** This
is the resolution of the discriminator ambiguity I escalated at v2 (a user map
`{value:9.99,type:"book"}` mis-read as a Data). Recognition is now strictly the `@schema`
marker via one recognizer (`IsDataMarked` / `HasDataMarker` / `IsWireShape` all key on it);
the `value+type` and `name+value` heuristics are deleted. `WireLocal` (Wire with `Sign=false`
+ `View.Store`) as Data's `[JsonConverter]` gives one canonical shape on every STJ path, with
the channel's options-registered signing `Wire` still outranking it on the wire (STJ ranks a
`Converters` entry above a type attribute — correct). `UnwrapJsonElement` lifts marked objects
back to Data on the universal parse path. `Data<T>` re-declares the attribute (type attributes
don't inherit through the generic) — good catch. No parallel wrapper type introduced; the
marker rides Data's own shape (no "Data is not enveloped" violation).

## Method note

The plang LLM planner mis-built every ad-hoc verification goal I tried (`add %b% to %a%`
compiled to `list.add(ListName="default", Value=null)`; `set item 2 of %a% = 99` compiled to
`variable.set(%a%, 99)` — replacing the whole list). A green ad-hoc `.test.goal` proves
nothing here. I confirmed F1 with a deterministic C# probe against the type directly, then
deleted it — `git status` clean, nothing committed beyond this report.
