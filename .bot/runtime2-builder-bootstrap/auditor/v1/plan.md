# Auditor v1 — runtime2-builder-bootstrap

## Context

Three reviewers approved this branch:

| Bot | Verdict | Coverage |
|---|---|---|
| codeanalyzer v4 | CLEAN | 5 files, deep behavioral pass on coder commit `65555d3e` |
| tester v4 | approved | C# 2309/2309 green; PLang 142/24-fail/4-stale; +21 unit tests; 4 mutation tests bit |
| security v1 | PASS | 2 medium info-disclosure, 4 low tripwires, no critical/high |

Acknowledged carryovers (deliberately scoped out):
- **F4 cluster** — 23 PLang test reds across Signing/Identity/UI/Event/etc. from the v2 builder squash `50351d8b`
- **Locale format-side asymmetry** — codeanalyzer v4 escalated; format sites still use Thread.CurrentCulture
- **promoteGroups** — unreachable from any goal; 0% coverage on its new ActionError path
- **ParamSnapshot bypasses [Sensitive]** — security F1, medium
- **Variables.Snapshot/GetAll standing finding** — security F2, medium

The branch is huge (2,379 files, ~30k insertions, 168 changed C# files, 14 new). My job is **not** to redo what they did — it's to find the spaces between.

## What I will check (the gaps between reviewers)

### A. Cross-file contracts not in any single-file review

1. **The clone family for Data/Step/Goal across the new properties.**
   Codeanalyzer flagged `Step.Clone()` missing 7 properties and `Data.Clone()` missing `_rawValue`; coder deferred both. But the auditor cares whether ANY consumer (test fixtures, MemoryStack flows, snapshot/restore paths) actually exercises these clones. If yes → real regression hidden by deferral. If no → safe.

2. **The new `IsCatalogDescription` helper at `DefaultBuilderProvider:655`.**
   Tester verified per-line coverage of this helper. But the helper exists to prevent a class of bug — does the **caller path** still have other ways to mis-route a literal value as a catalog description? Trace the caller and see what else could fool the gate.

3. **The new `enrichResponse` action — declared "build-time-only" in XML docs but not enforced.**
   The intentional `BuildingGuard` removal (security F6) means runtime-callable. The XML doc is not an enforcement mechanism. Worth checking whether security's "this is intentional" is actually intentional or a side-effect of the squash.

4. **TypeConverter parse-side InvariantCulture vs format-side Thread.CurrentCulture.**
   Codeanalyzer escalated this; tester noted "no non-Invariant culture test". The auditor angle: trace the @known round-trip path end-to-end across files and confirm the break shape codeanalyzer described. If real and reachable, severity should be major, not "deferred".

### B. Foundation ripple (changes to root objects)

`Data/this.cs`, `Variables/this.cs`, `LazyParamsGenerator.cs`, `TypeConverter.cs`, `Goal/Steps/this.cs`, `Errors/Error.cs` are foundation. A bug here flows downhill to every module. I'll spot-check that:

- `LazyParamsGenerator` emits the `__SnapshotParams` correctly across all property kinds (the security finding about [Sensitive] bypass — verify it's actually present in the emit, not theoretical).
- `Variables.Set` JSON deep-clone fallback path — security F3 flagged it as silent; codeanalyzer also flagged similar at `list/add.cs`. Are there OTHER set paths with the same pattern?
- `Data.@this` new `_rawValue` and `ResetResolution` interaction with handler restart paths.

### C. Test coverage gaps for new code paths

Per the new-code-path checklist (memory): for each new try/catch, if/else, switch branch — which test bites? The tester verified `IsCatalogDescription` and math `ExamplesForLlm`. The auditor checks the gaps:

- The 5 narrowed bare-catches (codeanalyzer #2): does any test exercise the catch *body*? Or are they fall-throughs that nobody tests?
- `NormalizeParameterTypes` returning `List<string>`: codeanalyzer traced surfacing to LlmFixer. But is there a test that proves the validation error round-trips back through `BuildStep`?
- `Debug.Apply` `_applied` idempotency guard: codeanalyzer says "no test bites" — confirm and decide if that's acceptable.
- `PromoteGroups` ActionError: 0% coverage confirmed. Should this block?

### D. F4 cluster — does it block?

23 PLang test reds. Coder explicitly scoped out. Tester carried over. Security didn't flag (out of scope). Auditor's question: **does any of the 23 reds expose a foundation bug** that affects the rest of the audit's confidence? I'll skim the failing test names to see if any look like a Data/Variables/Memory regression vs domain-specific (Signing, Identity).

If they're all domain-specific — defensible carryover.
If any look like foundation regressions — must block.

### E. Architectural fit

- Does the new `Catalog`/`PlangType`/`TypeConverter` split make sense relative to PLang's stated philosophy? Or is it complexity creep?
- The intentional `BuildingGuard` removal: does it widen the runtime trust surface in a way the threat model doesn't account for? Security flagged this at low. Worth a fresh read.
- The `enrichResponse` and `promoteGroups` modules — are they architectural debt (LLM-routable but unreachable) that should be deleted, or seeds for future work?

### F. Documentation/communication

- The original coder report described 3 small gaps. The actual branch is 2,379 files. Significant scope creep. Is what's here justified? Were the original 3 gaps actually fixed?

## What I will NOT re-check

- Per-file OBP analysis (codeanalyzer v1+v3 covered 167 files; v2/v4 verified fixes).
- Per-line coverage of the math `ExamplesForLlm` and `IsCatalogDescription` helpers (tester v4 mutation-tested these).
- The 6 security findings' attack surface enumeration (security v1 was thorough).

## How I will work

1. Skim git diff for the highest-risk foundation changes (~10 files). Verify reviewer claims about them.
2. Trace 2-3 cross-file paths end-to-end:
   - LLM → NormalizeParameterTypes error → validationErrors → BuildValidation → ApplyStep → HandleValidationError → BuildStep → LlmFixer (codeanalyzer claims this works).
   - LazyParamsGenerator.__SnapshotParams emit → Error.Params → Error.cs FormatVerboseValue stderr (security claims [Sensitive] is bypassed).
   - The locale @known round-trip path (codeanalyzer's escalated carryover).
3. Look at the 23 F4 reds — categorize by failure shape, not full debug.
4. Write `auditor-report.json`, `verdict.json`, `result.md`, `summary.md`.
5. Update bot-root `summary.md`, finalize report.json session, commit, push.

## Expected verdict

I expect **pass with carryovers** — the work is solid and four reviewers can't all be wrong about the same things. But I'm holding open the possibility that a cross-file gap or an F4 foundation regression flips this to fail. I won't rubber-stamp.

## Time budget

~30-40 min. If I find a single cross-file gap that the others missed, that justifies the audit. If I find none, the verdict is pass and I'll say so without padding.
