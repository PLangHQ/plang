# Auditor v1 — plan (singular-namespaces)

## What I'm auditing

The whole 4-stage refactor (singular rename, non-null invariants, accessor reshape,
type-entity move). Codeanalyzer reached v4 PASS on coder v2's fundamentals; tester
reached v3 PASS on coder v3 (test-only changes responding to tester v2). No security
review on the branch.

## Where I expect gaps

1. **Codeanalyzer v4 → v3 production diff.** v4 reviewed v2. Coder v3 changed only
   tests + `.pr` + Capture.goal — so v4's verdict still covers production. But its
   four minor/latent findings (F1 `IsNull` string-magic, F2 test-name overpromise,
   F3 `As(string)` fallback drop, F4 `Scheme` NRE) — F2 was addressed in v3 (test
   renamed); F1/F3/F4 are **still in HEAD as latent**. Codeanalyzer chose not to
   block; I'll spot-check whether any have promoted to real bugs after the broader
   reshape settles.
2. **No security pass.** Branch is mostly rename + non-null + entity move. The
   functional changes touching security-relevant surfaces are: `Permission.Find`
   producer-stamping rehydrated grants, `Promote()` throw error message exposing
   the type Value. Decide whether security routing is needed before docs.
3. **Cross-file contract: producer stamping.** Many `FromName(...)` call sites
   exist (module/list/*, module/builder/code, module/crypto/code, module/signing/Signature,
   module/file/read). Verify the Data setter propagation (`data/this.cs:243`) actually
   covers them, vs. leaving a context-less type entity exposed to a downstream
   `Fields`/`Values` read.
4. **OBP shape smells across the reshape.** 804 files changed. Spot-check that the
   type entity move + Entry fold didn't leave behind a courier reaching `.Value`,
   a flat-mirror class, or a cross-file lock target.
5. **Re-run the green claim.** Tester saw 3696/3696 + 253/253 (HTTP transients).
   Rebuild clean, run both.

## What I'm explicitly NOT redoing

- Per-file OBP review (codeanalyzer v1–v4 covered).
- Test honesty mutation checks (tester v1–v3 confirmed F1-RESIDUAL, N1, N2 mutations).
- File-by-file rename audit (tester baseline + codeanalyzer v1–v3 sealed Stage 1–3).

## Output

- `v1/result.md` — findings, cross-file traces, gap assessment
- `v1/verdict.json` — pass/fail + one-line
- `.bot/singular-namespaces/auditor-report.json` — structured findings
- Update `summary.md`
