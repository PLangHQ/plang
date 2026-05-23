# v2 review summary — what the coder was asked to fix

codeanalyzer v2 (`v2/report.md`, commit `eb85fcbd`) verified that all eight v1
findings (F1–F8) were genuinely fixed, then raised three new findings:

- **N1 (Medium)** — the F3 refactor silently dropped `file.exists`'s `Read`
  authorization gate. `FilePath.AsBooleanAsync` had become an ungated
  `File.Exists` probe; out-of-root existence probing went silent and asymmetric
  vs `HttpPath.AsBooleanAsync` (which gates). A permission gate should not
  change by side effect — needed an explicit, recorded decision.
- **N2 (Low)** — `path.Equals` / `GetHashCode` still hard-coded
  `OrdinalIgnoreCase`, the exact case-sensitivity drift F5 fixed in `Relative`
  by introducing `RootComparison`.
- **N3 (Low)** — `assert.ResolveTruthy` re-implemented the `IBooleanResolvable`
  dispatch instead of reusing `Data.ToBooleanAsync()`.

Verdict was **NEEDS WORK** (a small one — N1 the only substantive item).

The coder addressed all three in commit `a1c3f9563` ("coder: address
codeanalyzer v2 findings N1-N3"). Ingi's recorded decision on N1: gate it.
v3 of this review verifies those three fixes.
