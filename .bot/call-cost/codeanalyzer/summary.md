# codeanalyzer summary — call-cost

**Version:** v1. Verdict: PASS, tester next (stage 1, 77add2895..4dcfa3f36).

**What this is:** the call-cost branch makes the runtime cheaper per call, stage by stage from the measured ideas (`.bot/event-action/codeanalyzer/call-cost.md`). Stage 1: the step cache made at its first ask, a frame's `Parameter`/`Tags`/`Errors` made at their first ask, `goal.Broken()` judged once.

**What was done:** bench at both hashes. Sub birth 101 → 54 KB, sub request 441 → 371, root birth 2260 → 2195, root request 3897 → 3832, a goal run again (`repeat`) 227 → 201 KB, held flat. Review in `review.md`: four low notes (the lazy idiom written four times, the cache seam's stale comment, `Broken` judged once against a mutable step `Error`, `Children`/`property` still eager per frame).

**Next:** measure each stage on its own hash (`Tools/Bench/run.sh`, `Bench repeat`, `Bench phases`); stage 2 is the coder's now.

**Example:**
```csharp
public Errors => _errors ?? Interlocked.CompareExchange(ref _errors, new(_stack.Errors), null) ?? _errors;   // made by its first asker; a reader that only looks reads the field
```
