# codeanalyzer summary — `compare-redesign`

**Version:** v1 · **Verdict: NEEDS-FIX (FAIL)** → next bot: **coder**

## What this is
First codeanalyzer pass on `compare-redesign`, run as a **swallowed-error scan**
after the user flagged `var (convEl, _) = TryConvert(...)` in the catalog
conversion path ("error is swallowed, you should have caught this"). Two scopes:
the branch's own production diff, and a full-codebase scan for discarded `Error`
tuple slots / empty catches.

## Result
- **Branch diff: clean.** The 4 changed production files (`Wire.cs` fail-closed
  hardening already security-reviewed, `serializer/plang` doc update, `Operator.cs`
  dead-`BothPresent` delete, `Comparison.cs` cosmetic reorder) introduce no
  swallow. Build green (0 errors).
- **F1 (MAJOR, systemic, pre-existing):** catalog `TryConvert`'s `Error` slot is
  discarded across the conversion read/write path. The flagged
  `Conversion.cs:229` inserts the **unconverted** value into a typed `list<T>`
  (`convEl ?? row.Peek()`) — the element-type invariant is silently broken.
  Siblings at `:361`/`:375` silently drop failed elements; `dict/this.cs:190`
  returns null; `variable/list/this.cs:420/439/457/534` keep the wrong-typed value
  and let a reflection `ArgumentException` replace TryConvert's slot-named error.
  The same file's `IList` arm (381–409) and `variable/set.cs:63` already handle
  this error correctly — *same-shape-second-site with a correct sibling = FAIL.*
- **F2 (MINOR):** `builder/code/Default.cs:608` discards `GetCodeGenerated`'s
  `IError`; build pass silently skips an unresolvable action.
- **F3 (MINOR):** `actor/this.cs:127` discards `Code.Get<IIdentity>()` error;
  `%MyIdentity%` collapses to null with no diagnostic.

## Routing
F1 fix is code authoring (propagate the error / aggregate per-element like the
IList arm; stop writing unconverted values into typed slots) → **coder**.

Full detail + line cites + cleared non-issues: `v1/report.md`.
