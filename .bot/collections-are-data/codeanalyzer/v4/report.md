# Code Analyzer — collections-are-data — v4 (re-review)

**Verdict: PASS** · **Next bot: tester**

Re-reviewed `e4974bcad..fecbb4dfb` (coder v6). Clean rebuild, both suites green:
**C# 4089/0** (+2 tests), **plang 273/273**, build 0 errors. All actionable v3 findings
resolved; F2 stays the documented deferral with its merge gate intact.

## v3 findings

- **F1 (blocking, list aliasing) — FIXED, and verified with teeth.** `add`/`set` now
  structure-copy a list value at the merge boundary via `list.@this.CopyStructure()`
  (`list/this.cs:148`) — a new list with its own rows, recursive on nested lists so no
  mutable list structure is shared at any depth; leaf/dict element Data are shared by
  reference, which is safe because `set %x% = …` rebinds rather than mutates. Applied in the
  two handlers that store a user value into a list (`add.cs:37`, `set.cs:24`); scalars/dicts
  stay by-reference. O(k) in the *added* list, never reads the existing rows — the
  architect's "append, don't touch existing" contract holds.

  The coder turned my v3 probe into a permanent end-to-end test,
  `ListTests.Add_List_DoesNotAliasSourceVariable` — drives the real `add` action with `%b%`
  in a second variable and asserts independence **both** directions (write-through: `set`
  into `%a%` leaves `%b%`; read-view: `%b%.add` doesn't change `%a%.count`). I deletion-tested
  it: reverting the fix to the old by-reference store fails that one test and only that one,
  then passes again on revert — so the test genuinely guards the regression, not a false green.

  Scope check: the write-through bug only ever applied to list rows (the only thing `Locate`
  descends into); dict/scalar elements are weight-1 and never descended, so leaving them
  by-reference is correct. The other list-builders (`where`/`group`/`unique`/`map`) yield
  flattened leaf/dict Data, never a shared mutable *list* row, so they don't need the copy.

- **F2 (major, disabled signing tests) — unchanged, accepted deferral.** Still the two
  `Tests/LazyDeserialize` goals, disabled pending `signature-as-schema-wrapper`. Documented
  (report, todos, branch spec). **Merge gate stands: do not merge this branch to `main` ahead
  of `signature-as-schema-wrapper`**, or `verify` of a signed value crossing a
  goal/list/store boundary is broken in the product. Flagging for tester to confirm the
  disable is exactly those two goals and that the C# verify-against-raw probe still covers the
  primitive.

- **F3 (perf, O(n²)) — FIXED.** `AreEqual`/`Order` materialize both flattened views once and
  index off the locals; `Remove` scans `Items` once then a single `RemoveAt`; `unique`
  accumulates into a plain `List<Data>` so the inner dup-scan no longer re-materializes
  `Items` each outer iteration. Linear walks instead of quadratic.

- **F4 (nits) — FIXED.** `Wire.cs` `LiftDataIfShaped` comment rewritten (no longer describes
  the deleted name+value sniff); `dict/this.cs` `catalog`→`list`; `scheme=data`→`@schema:data`
  across all recognizer comments, matching the `WireSchema = "@schema"` constant.

## Method

Clean rebuild from scratch; both suites. F1 fix read end-to-end (CopyStructure recursion +
both call sites + scope of by-reference leaves) and confirmed by deletion test on `add.cs`
(announced, reverted, `git status` clean). The branch is sound; the only thing left between
it and `main` is the F2 signature redesign on its sibling branch.
