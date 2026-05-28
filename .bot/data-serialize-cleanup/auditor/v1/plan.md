# Auditor v1 plan — `data-serialize-cleanup`

**Date:** 2026-05-28
**Branch:** `data-serialize-cleanup`
**Status entering audit:** codeanalyzer v2 PASS, tester v2 PASS, security v2 PASS (F1 retracted HIGH→Info after mutation test). No coder/ folder was written (process gap; tester flagged it).

## Approach

Read all prior bot reports first. Trust their slice; look at the seams between them.

1. Confirm the three PASSes by re-running their load-bearing claims:
   - Codeanalyzer v2: each F1–F11 closure landed as described — spot-check at the cited line numbers.
   - Tester v2 mutation: confirm Stage 2 canonicalization test would fail if `crypto.Hash` swapped `OutboundOptions` for `JsonSerializerOptions.Default`.
   - Security v2: confirm `LiftDataIfShaped` recursion bound is the per-reader `MaxDepth=64` inheriting through `ParseValue`, not the new `AsyncLocal<int>` counter.

2. Cross-file contract walk — the gaps codeanalyzer's file-by-file pass would not catch:
   - `WireJsonConverter` ↔ `crypto/Default.cs` ↔ `plang.@this.OutboundOptions` ↔ `ContextLessFallback`: does the canonicalization story hold across the four files, including under the type-mismatch error path?
   - `Properties` insertion gate ↔ wire emit ↔ wire read ↔ navigation: what shape can be smuggled and how does it round-trip?
   - `Variable.Resolve` ↔ `variable.set` ↔ `Properties[]`: does the malformed-shape gate compose with the property write path?

3. Architectural fit:
   - Does `ContextLessFallback` belong as a `public static readonly` on plang.@this, or is it leaking the inter-actor surface? Confirm acyclic construction.
   - `Unwrap` documented Context-mutation tradeoff (F11): real today, or contained?

4. Foundation ripple:
   - `Data` ↔ `Properties` is foundation. Any v1/v2 fix that breaks a downstream invariant nobody re-checked?

5. New-issue scan limited to *branch additions only* (not the runtime2 merge baseline) — bare catches, System.IO, Console.*, sync-over-async sites not on the F3 carve-out.

## Expected outputs

- `v1/result.md` — findings, ranked by blast radius
- `v1/verdict.json` — pass/fail
- `.bot/data-serialize-cleanup/auditor-report.json`
- Update `summary.md` and `report.json`, then commit + push

No implementation. This is review only.
