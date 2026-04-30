# Auditor v1 — plan

## Task

First auditor pass on `runtime2-generator-obp`. The branch went through:
- architect v4 (resolution as read transformation; Data stateless)
- coder v1–v5 (5 rounds)
- codeanalyzer v1 (NEEDS WORK), v2 (NEEDS WORK), v3 (CLEAN, on coder/v3)
- tester v3 (NEEDS WORK), v4 (APPROVED, on coder/v4)
- security v1 (PASS, on coder/v4) — 1 medium + 3 low
- coder v5: closes security #1 (`[Sensitive]` masking) + #3 (cycle/depth → ServiceError)

**Coder/v5 has not been reviewed by codeanalyzer or tester.** I'm the first
reviewer who sees the v5 delta. My auditor angle is the whole-picture view:
cross-file contracts, foundation ripple, review gaps between bots.

## What other bots already covered

- **codeanalyzer/v3** (verifies coder/v3): cycle / depth / OCE asymmetry /
  diagnostic span all clean. Production code clean, deletion-tested.
- **tester/v4** (verifies coder/v4): Pattern B regex widened to
  `public|protected`, all helpers + integrated regex empirically pinned.
  C# 2466/2466 green.
- **security/v1** (on coder/v4): `Data.As<T>` resolution path safe (cycle +
  depth), no JSON depth issues, `App.Run` catch filter correct. Snapshot
  masking gap (Medium #1) and cycle/depth-trip silent return (Low #3) are
  the two findings coder/v5 fixes.

I trust their work. I will not redo their passes.

## Auditor focus — gaps between reviewers

### 1. Cross-file contracts of the v5 delta

**`SensitiveAttribute` plumbing** — `Discovery` reads a *new* attribute and
threads `IsSensitive` through `DataProperty` and `LegacyProperty` records.
- Are all consumers of these records updated for the new ctor field?
  (`IncrementalCacheTests` adds it to 4 ctors; are there others?)
- Does the `IsSensitive` field participate in record equality the way the
  incremental-cache contract requires? (codeanalyzer v2 found that
  `class` → `record` was the cache fix; adding a field doesn't break that
  but it must equate properly between runs.)
- Is the attribute name match (`"SensitiveAttribute"`) consistent with how
  the runtime's `SensitivePropertyFilter` recognizes it? Same convention
  used by `ProviderAttribute` / `VariableNameAttribute` per the comment.

**`Data.AsT_Impl` cycle/depth → `ServiceError`** — the contract changed
from "return strVal as-is, Success=true" to "return ServiceError,
Success=false". This is a behavior change visible to every caller.
- Who calls `Data.As<T>` and assumes Success-only flow?
- Does the generated property code (line 32, 36, 40, 44 of
  `Emission/Property/Data/this.cs`) propagate this error or silently
  drop it via `(T?)result.Value`?
- Existing handlers (e.g. `output/write`, `variable/set`) — do any
  rely on the old swallow-and-pass-through to keep running through a
  cycle? This is the "review gap" risk.

### 2. Architectural fit

- Does `[Sensitive]` masking belong in the generator emission, or should
  it sit in `App.Errors.ParamSnapshot.ToString()` / Error formatting?
  Generator-side mask is correct only if the caller-side never inspects
  `ParamSnapshot.PrValue` for non-display purposes.
- Does the `"******"` mask token collide with any value that legitimately
  appears in PrValue (e.g. a literal `"******"` for a UI placeholder)?

### 3. Review-quality and deletion-tested claims

- Coder/v5 says cycle test renames retain the "must not stack-overflow"
  guarantee. Verify: do the new tests actually run AsT and confirm no
  StackOverflowException, or do they only check `Error.Key`?
- The `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue`
  test name says "unaccessed" but the handler always touches both
  properties. Does the test actually exercise the unaccessed branch
  (`PrValue=null` with no FinalValue), and does the assertion pin the
  null-guard correctly?

### 4. Foundation ripple — `Data.cs`

`Data.cs` is the universal result type. A change to its resolution path
ripples through every handler. I'll check:
- Cycle path returns *before* try/finally — does this leak the outer
  frame's HashSet entry across calls? (Verified by codeanalyzer/v3 for
  the depth path; does the same reasoning hold for the cycle path?)
- HashSet lifecycle: cycle path doesn't Remove because outer owns; depth
  path enters try, returns inside try, finally Removes. Correct?

### 5. The standing observation from tester/v4

> "Strip integration into `LoadAllCallableSources` is not directly
> pinned" — listed as observation, not finding, with precedent reasoning
> (Pattern A has the same gap).

I'll either agree (precedent-aligned) or escalate to a finding.

## Process

1. Read full `Data.cs` resolution path (lines 380–510).
2. Read both emission files end-to-end.
3. Cross-file: who uses `data.As<T>().Value` without checking `.Success`?
4. Build + run tests locally to confirm the 2468/2468 claim is honest.
5. Spot-check generated source for `SensitiveSnapshot` to confirm the
   masked expression is wired correctly per coder/v5's spot-check claim.
6. Write `auditor-report.json`, `verdict.json`, `summary.md`.

## Deliverables

- `.bot/runtime2-generator-obp/auditor/v1/plan.md` (this file)
- `.bot/runtime2-generator-obp/auditor/v1/result.md`
- `.bot/runtime2-generator-obp/auditor/v1/summary.md`
- `.bot/runtime2-generator-obp/auditor/v1/verdict.json`
- `.bot/runtime2-generator-obp/auditor-report.json`
- `.bot/runtime2-generator-obp/auditor/summary.md` (cross-version index)
- `.bot/runtime2-generator-obp/report.json` (append session entry)

## Suggested next step (preview)

If pass: docs bot. If fail: send findings to coder.
