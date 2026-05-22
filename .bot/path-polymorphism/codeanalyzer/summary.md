# codeanalyzer — path-polymorphism

**Version:** v3

## What this is

`path-polymorphism` makes PLang's `path` type scheme-polymorphic: an abstract
`path` base with `FilePath` / `HttpPath` subclasses, a per-App scheme registry,
collapsed file handlers, and the deleted `System.IO.Abstractions` wrapper layer.
codeanalyzer reviews the C# for OBP compliance, simplicity, and silent-failure
risk.

## What was done

**v1** reviewed the initial branch — NEEDS WORK, 8 findings (F1 High, F2–F4 Med,
F5–F8 Low).

**v2** re-reviewed coder's response to F1–F8 — all eight genuinely fixed.
NEEDS WORK with three new findings: N1 (Med — `file.exists` lost its auth gate
as a side effect of the F3 refactor), N2 (Low — `path.Equals`/`GetHashCode`
case drift), N3 (Low — assert truthiness duplication).

**v3** (this version) re-reviewed coder's response to N1–N3 (commit
`a1c3f9563`). **All three genuinely fixed:**

- **N1** — `FilePath.AsBooleanAsync` now routes through the gated `ExistsAsync`
  (was an ungated `File.Exists`). A denied out-of-root probe answers `false`;
  structurally identical to `HttpPath.AsBooleanAsync` — the per-scheme auth
  asymmetry is gone. Ingi's recorded decision: gate it. New test
  `FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse` is a real
  regression guard.
- **N2** — `path.Equals`/`GetHashCode` switched `OrdinalIgnoreCase` →
  `RootComparison`, the rule `Relative`/`IsUnder`/`ValidatePath` already share.
- **N3** — `assert.ResolveTruthy` delegates the `IBooleanResolvable` branch to
  `Data.ToBooleanAsync()` instead of duplicating the dispatch; the plain-value
  `IsTruthy` branch (string-`"false"` semantics) stays, deliberately.

Build clean (0 errors). C# 2882/2882. plang 203/203, 0 stale — fully green, no
external flake this round.

**Verdict: CLEAN.** No new findings. The path-polymorphism branch is sound.

## What to do next

Run the tester. The branch is ready for behavioral validation.

## Code example

The N1 fix — file truthiness now goes through the same gated path as http:

```csharp
// types/path/file/this.Operations.cs — was an ungated File.Exists probe
public override async Task<bool> AsBooleanAsync()
{
    var existsResult = await ExistsAsync();   // ExistsAsync runs AuthGate(Read)
    return existsResult.Success && existsResult.Value is true;
}
```

A denied probe → `Success == false` → `false`. Same shape as
`HttpPath.AsBooleanAsync` — the per-scheme asymmetry v2 flagged is closed.
