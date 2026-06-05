# Code Analyzer — collections-are-data — v1

**Verdict: NEEDS WORK** · **Next bot: coder** (resolve F1 + F2, then back to codeanalyzer for re-review)

Reviewed `6b9b60976..2defdfc5f` (runtime2 base → branch HEAD). C# production diff: 63 files,
~2000 insertions. Both suites green from a clean rebuild: **C# 4080/0**, **plang 273/273**
(the `total: 104/132` line is action *coverage*, not pass/fail — 273 `[Pass]`, zero failures).
Build clean, 0 errors; the 282 warnings are pre-existing nullability noise; the diff adds **no**
new `System.IO`/`Console`/PLNG violations, and `Data.Value` courier reaches stay within leaves.

## The core feature is sound

The headline deliverable lands correctly. `dict`/`list` are native `Data`-holding value types
(`type/dict/this.cs`, `type/list/this.cs`), the parse seam builds them once
(`UnwrapJsonObject`/`UnwrapJsonArray` → native, no decompose), the navigators collapse onto the
types (`WrapItem` deleted), `set` rebinds instead of mutating (`variable/list/this.cs:199,231` —
faithfully mirrors the Data-value branch incl. subscriber-carry), and the `application/plang` wire
round-trips signed elements (`Wire.LiftArrayElements`, `Load()`/`Normalize` walk the new shapes).
F1-the-original is dead. No objection to Stages 1–3.

The findings below are all on the **Stage 4–5 "one compare path" goal**, which the plan stated as
"`if a > b` and `sort by …` can never drift." It does drift, in the new code, in ways the green
suites don't catch — which is exactly the silent-inconsistency class I'm obligated not to wave
through.

---

## F1 — `text` equality is case-insensitive but `text` ordering is case-sensitive (blocking)

`app/data/Compare.cs`:
- `Order` text arm (line 59): `string.CompareOrdinal((string)lv,(string)rv)` — **case-sensitive**.
- `AreEqual` text arm (line 98): `string.Equals(ls,rs,OrdinalIgnoreCase)` — **case-insensitive**.

So `"a" == "A"` is **true** while `"a" > "A"` is also **true** (`CompareOrdinal` = 97−65 > 0).
Trichotomy is violated: a value is simultaneously *equal to* and *greater than* another. Concretely,
`unique` (routes `Compare.AreEqual`) dedupes `["Apple","apple"]` to one, but `sort` (routes
`Compare.Order`) orders them as distinct — a sort+unique pipeline behaves inconsistently. The impl
also contradicts its own doc comment (`Compare.cs:13`: "text lexical/**invariant**") — `CompareOrdinal`
is neither invariant-culture nor ignore-case.

**Fix:** make the `Order` text arm agree with equality —
`string.Compare(a, b, StringComparison.OrdinalIgnoreCase)` (or pick one case-policy and apply it to
both arms). One policy, one path.

## F2 — `contains` / `in` use a second, divergent equality (blocking)

`app/module/condition/Operator.cs`: `==`/`!=` route through `global::app.data.Compare.AreEqual`
(line 105), which compares dict/list **structurally**. But `contains`/`in` route through the local
private `Operator.AreEqual` (line 108, called from `ContainsElement` line 150), which does only
`NormalizeTypes` + string-ignorecase + `.Equals` — **reference** equality for dict/list.

Result: `%list% contains %dict%` returns **false** for a structurally-equal dict, while
`%elem% == %dict%` returns **true**. Two equality implementations that drift — the precise smell
this branch set out to delete, reintroduced inside the modified file.

**Fix:** route `ContainsElement` through `Compare.AreEqual` and delete the local `Operator.AreEqual`.

---

## F3 — dead code: `Operator.Compare(object?, object?)` (cleanup)

`Operator.cs:122`. After ordering relocated to `Compare.Order`, this private method has **zero
callers** repo-wide (verified — the `Compare(...)` calls in `assert/code/Default.cs` resolve to
assert's own local helper at line 185, not this). Leftover of the old path; remove it before a future
caller wires onto the wrong semantics.

## F4 — legacy `List<object?>` branches in `sort`/`unique` bypass the one-compare-path (cleanup / latent divergence)

`list/sort.cs` legacy branch uses `Comparer<object>.Default`; `list/unique.cs` legacy uses
`list.Distinct()` (default equality). Where a variable still holds a raw `List<object?>`, these
diverge from `Compare.Order`/`Compare.AreEqual` — nulls-last, mixed-type-throw, and structural
equality are all lost. Either these raw-list arms are now dead (parse seam + `add` + every op produce
native lists → delete them) or they're reachable and silently divergent. Per "don't downgrade a
second-site divergence," resolve it rather than leave both. (`set`/`remove`/`reverse` legacy arms are
mutation-only — no compare semantics — and are fine to keep as fallbacks or sweep alongside.)

## F5 — `Wire.IsDataShaped` name+value heuristic is fragile (info)

`Wire.cs`: on the `application/plang` array-read path, any object element bearing both `name` and
`value` keys is lifted to a Data envelope. Safe for the wire it serves (the writer always envelopes
list elements, so bare `{name,value}` objects never appear there), but the discriminator keys off two
*user-pluggable* field names rather than the envelope's own markers. Prefer keying on `type` (and/or
`signature`) presence — the slots a genuine envelope always carries and a user payload usually
doesn't. Latent trap only, not a live bug.

---

## Note (not a finding, for the coder's awareness)

The compare unification is **partial by plan scope**: plan row G named only "the condition operators
**and** `data.sort`." `assert.greaterThan`/`assert.lessThan` still run their own private `Compare`
(`assert/code/Default.cs:185`: `Convert.ToDouble` → `IComparable` → `string.Compare Ordinal`) —
a path that can disagree with `if a > b` on datetimes and mixed types. This is pre-existing (assert is
outside this branch's diff) and arguably out of scope, but it means "one compare path" is aspirational
until assert is folded in. Flagging so it's tracked, not silently assumed unified.

## What to do

- **F1, F2** — resolve (the blocking pair). Both are inside the new `Compare.cs`/`Operator.cs`.
- **F3, F4, F5** — fold in while you're there; F3/F4 are small, F5 is a one-line discriminator swap.
- Re-run both suites; hand back to **codeanalyzer** for re-review.
