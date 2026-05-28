# Security v2 — `data-serialize-cleanup`

**Date:** 2026-05-28
**Branch:** `data-serialize-cleanup`
**Range:** `115ca13da..f5b4ae3a7` — coder's F1 fix.

## Scope

Verify Coder addressed F1 (HIGH from v1). Read the diff, run the regression tests, and mutation-test whether the new gate is load-bearing.

## What I found

### F1's actual exploitability — corrected from v1

Mutation: temporarily set `MaxReadDepth = int.MaxValue` and re-ran the 200-level depth-bomb test.

**Result: the test still PASSED.**

That tells me the new AsyncLocal counter is not what's rejecting the bomb. STJ's per-reader `MaxDepth=64` (default on `JsonSerializerOptions` and on `JsonDocumentOptions` used by `ParseValue`) catches the payload first.

The corrected trace:

- Outer `Deserialize` creates a `Utf8JsonReader` with `MaxDepth=64`.
- `ReadBody` sees `value: StartObject` → `JsonDocument.ParseValue(ref reader)`.
- `ParseValue` consumes tokens from the **same** reader. The reader's own MaxDepth self-enforces — `Utf8JsonReader.Read()` throws `JsonException` when `CurrentDepth` would exceed `MaxDepth`.
- For a 200-deep payload, `ParseValue` throws at depth 64 inside the **outermost** call. No `LiftDataIfShaped` recursion happens.
- For a 64-deep payload (the deepest the reader will accept), each `Deserialize<@this>(rawText)` recursion gets a sub-tree shrinking by ~1 level — so the C# recursion ladder shortens, not grows.

**My v1 reasoning gap:** I treated each `LiftDataIfShaped` → `Deserialize<@this>(string)` as "depth-reset → unbounded recursion." That's true for the new reader's depth budget, but `ParseValue` can't hand the recursive call a sub-tree deeper than the source reader's MaxDepth let through. So the recursion ladder is bounded by ~MaxDepth × constant, not by unbounded payload depth.

**Verdict on F1:** false-positive at HIGH. STJ defaults (MaxDepth=64 on every reader options STJ constructs) already protect the deserializer. The coder fix is defense-in-depth — useful if a future caller raises `options.MaxDepth` to allow deeper non-Data graphs — but doesn't close a real exploitable vulnerability.

### Coder's fix quality

Code is correct in shape — `AsyncLocal<int>` counter, increment at the entry, decrement in `finally`, `ReadBody` extracted for single-source-of-truth discipline. No regressions in C# (3232/3232) or PLang (228/228). Tests cover both string-entry and stream-entry paths. Keep it as defense-in-depth.

### F2 / F3 / F4 — unchanged

Still standing open as Low / Info per v1. No new mitigation in this commit; coder explicitly punted each to a separate change.

## Verdict

PASS — but with the v1 HIGH retracted as a false-positive. The branch was actually mergeable at v1; my severity flip was based on a reasoning error. Honest correction lives here so the lesson sticks.

## Lessons added to memory

Updated `feedback_pre_auth_parse_severity.md` with the missing trace-step (ParseValue inherits the source reader's MaxDepth; recursion through `LiftDataIfShaped` is bounded by MaxDepth × constant, not by payload depth). The original lesson about "pre-auth, unrecoverable, reachable today" still holds — just confirm the actual stack-blow path is unbounded before rating HIGH.
