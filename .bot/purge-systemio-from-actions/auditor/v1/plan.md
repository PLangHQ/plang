# auditor v1 — plan

**Branch:** `purge-systemio-from-actions`
**Reviewed version:** v2 (post-security-v2 PASS)
**Diff:** `git diff runtime2..HEAD -- ':(exclude).bot'` — 108 files, +4729/-781

## What the three prior bots already covered

| Bot | Verdict | Coverage |
| --- | --- | --- |
| codeanalyzer v1 | PASS (low only) | OBP/D13/Execute-verb/AppGoals indexing/PLNG002 alias-robustness — 5 LOW findings (N1–N5). |
| tester v2 r2 | PASS | 4 handler-layer false greens fixed; coverage on cold files lifted (Fluid 42.6→73.4%, Debug 35.2→50.9%). Claims **C# 3025/3025, PLang 206/206**. |
| security v2 | PASS | F1 (HIGH IsInRoot bypass) closed by ctor canonicalization; F2 (MarkdownTeaching) closed by path-verb lift; F3 (`app/this.cs` exemption) closed. |

## What I'm checking — the gaps between them

1. **Was the tester's PASS real?** Rebuild clean from a blank `bin/`/`obj/` and rerun *both* suites. Stale-binary trap is the obvious risk on a branch this large.
2. **Did codeanalyzer's N4/N5 latent issues stay latent?** Read `AppGoals` after coder N1/N5 commit.
3. **Implicit `string → path` operator (codeanalyzer N3 footgun) — is the silence still safe?** Audit production callers that pass a `string` to a `path` slot.
4. **PLNG002 narrowing soundness** — `IsPathHelperFile` vs `IsPathTypeSurface` carve-out logic against the actual production surface.
5. **The F1 fix surface area** — canonicalization at ctor inherits everywhere. Anyone in PLang ever rely on un-canonicalized paths surviving into `_absolutePath`? The F1 mitigation is the v2 change with the most ripple.

## Verdict gate

PASS unless (a) tests don't actually pass, or (b) a cross-file contract is broken.
