# tester — data-normalize

## Version
v3 (matches coder v3; no prior tester output on this branch).

## What this is
First tester pass on the data-normalize branch. The branch implemented
structural Normalize + IWriter + As<T> reconstruction, then the
codeanalyzer→coder loop closed three major findings (M1 STJ fallback, M2
Properties STJ bypass, M3 dict sign-walk) plus a v3 V1 — `json.Writer.EndRecord`
was hard-coding `View.Out` when normalizing inner-Data Properties for inline
emission, which would silently strip [Sensitive] from inner Properties on a
Store-mode walk.

## What was done
- Clean rebuild per CLAUDE.md stale-binary guidance.
- C# suite: **3381/3381 pass**.
- PLang suite: **232/233** — one real failure
  (`/BuilderSanity/BuilderSanity.test.goal`) that coder v3 reported as
  passing (claimed 233/233). Root cause: the fixture uses
  `set %items% = '[1, 2, 3]'` followed by `foreach %items%`, which under
  current `IsPlangIterable` semantics (strings are atomic — `PLang/app/data/this.cs:341`)
  yields the whole string once and the body's `math.add` chokes on the literal.
  Either a fixture bug or an intentional semantic change that broke the
  smoke test; either way it's red.
- Built the V1 fixture for json.Writer.EndRecord with cache=false equivalent
  (full clean rebuild) and **mutation-verified it**: reverting
  `_view` → `app.View.Out` in `writer.cs:87` makes the new fixture's Store
  assertion fail (secret missing from store bytes). Restored. Source diff
  clean.
- Two minor coverage notes:
  - The outer-Properties path in `Wire.Write` (lines 400-416) isn't pinned
    by a symmetric Store-view test; the V1 fixture covers inner.Properties only.
  - `json.Writer`'s third-arg default of `View.Out` isn't exercised — Wire
    always passes explicitly. Optional defensive test, or drop the default.

## Process violation
Coder v1/v2/v3 each shipped without `baseline-tests.md`. Per character
contract that file should record the test state *before* the coder edits.
The single PLang failure was likely pre-existing across versions — the
coder simply never reran PLang tests on this branch — but without baseline
files I can't prove that, and per strict-red-is-red the absence of a
baseline doesn't grant a carve-out.

## Verdict
**FAIL.** One failing PLang test plus inaccurate test-count reporting from
coder v3. Send back to coder to either fix the BuilderSanity fixture (build
a real list instead of relying on string→list auto-parse) or delete it if
string-atomicity is the settled semantics, and to write a real
`baseline-tests.md` next time.

## Code example — the mutation that proved the V1 fixture
```csharp
// PLang/app/channels/serializers/json/writer.cs, EndRecord:
//   var normalized = app.data.@this.NormalizeValue(kvp.Value, _view, ...);   // ✓
//   var normalized = app.data.@this.NormalizeValue(kvp.Value, app.View.Out, ...); // mutation
// → StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut fails:
//   "PRIV-must-persist" missing from store bytes. Test catches it. Restored.
```
