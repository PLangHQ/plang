# auditor — runtime2-cleanup

## Version

v1 — single audit pass at end of branch.

## What this is

The runtime2-cleanup branch is a 27-stage, 107-commit structural OBP refactor.
Architect carved each stage; coder shipped 27 commits; codeanalyzer v3 did a
cumulative final pass (PASS, 3 non-blockers); security v1 did a trust-boundary
sweep (PASS, 2 low — both echoing codeanalyzer hygiene items). My job: the
seams between those reviewers.

## What was done

Read codeanalyzer v3 + security v1 + architect's results.md. Did **not** redo
the file-level OBP sweep or the crypto/trust pass. Looked at:

1. **Cross-file contracts** — same-thing-stored-thrice patterns.
2. **Architectural fit** — Tier 5's static-vs-instance judgment calls.
3. **Process gaps** — no tester report; what does that miss?
4. **Anti-thematic carry-overs** — a Console.Out.Write in test/report.cs that
   contradicts the branch's own thesis of channel discipline.

Verified: clean rebuild + both test suites green at `fb8eda3b`
(C# 2752/2752, PLang 199/199).

## Findings

Four minor — none block merge.

| # | Where | What |
|---|-------|------|
| 1 | `TypeMappingTestFacade.cs:67` | `Json.CaseInsensitiveRead` is a fourth fork of the same JsonSerializerOptions, not routed to either production home — silent drift risk on future converter additions. Other facade properties route correctly; only this one doesn't. |
| 2 | `Diagnostics/this.cs:21` | Declared `public static class @this`. No `app.Diagnostics` exists. Static @this dilutes the convention's signal. |
| 3 | `test/report.cs:38` | `Console.Out.Write` — already flagged by codeanalyzer v3-2 / security #2 as "non-blocker / accepted-risk". I'd elevate: anti-thematic to a cleanup branch whose stated purpose includes channel discipline. One-line fix. |
| 4 | (process) | No tester report on a 465-file branch. Most facade tests route to production, so real coverage exists, but advisory for next branch. |

## Verdict: FAIL — coder fix pass requested before merge

Findings 1, 2, 3 are quality issues but Ingi opted to fix them pre-merge rather
than carry them forward. Finding 4 (no tester report) is process-only and does
not require a code change.

**For the coder:**

1. **Finding 1 — `TypeMappingTestFacade.cs:67`.** Route `Json.CaseInsensitiveRead`
   to a production home so the facade tests pin the production options. Either:
   (a) expose an `internal static JsonSerializerOptions CaseInsensitiveRead` on
   `App.Types.Conversion` (or a similarly scoped sibling) and forward; or
   (b) accept the duplication explicitly with a comment block at all three sites
   naming the other two — but (a) is preferred.
2. **Finding 2 — `Diagnostics/this.cs:21`.** Either rename the class away from
   `@this` (e.g. `App/Diagnostics/Format.cs` with `static class Format`) so
   `@this` stays reserved for instance navigation; or mount as `app.Diagnostics`
   with an instance form so the convention is honoured. Coder/Ingi pick.
3. **Finding 3 — `test/report.cs:38`.** Replace `Console.Out.Write(console.ToString())`
   with `await Context.App.CurrentActor.Channels.WriteTextAsync(Output, console.ToString())`,
   mirroring the pattern in `output/write.cs`. Single-line change.

After fixes: keep both test suites green (2752/2752, 199/199), then auditor v2
re-runs.

Architect's audit, codeanalyzer's verdict, and security's verdict on the
underlying refactor all stand — these are end-of-branch polish items.

## Files

- `v1/plan.md` — audit approach
- `v1/result.md` — full findings + reasoning
- `v1/verdict.json` — `{ status: "pass" }`
- `../auditor-report.json` — structured report
