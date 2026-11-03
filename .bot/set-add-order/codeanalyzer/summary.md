# codeanalyzer — set-add-order

**Version:** v1 (f3d89adf6) — FAIL, coder next.

**What this is.** `set %x% = %x% + 1` built `variable.set; math.add` and lost the sum, because the pick list marked math.add only
Possible: `Sure` read math's share of stage 1's one-of "main work" choice. The coder's fix: `Part(module)`, a module's share over
the summed shares of the modules no near-certain common action settles, read by `Certain` (was `Sure`).

**What I found.** `Part` assumes the shares are the whole distribution. With no `probabilities` in the stage-1 answer the
decider's fallback leaves one entry, `Part` returns 1.0, and `remove %x%` turns Certain (probe, deleted): the 09-30 guard
broken. Fix: divide by 1 less the settled shares (`Left`), which also drops a dead guard. Minor: regex for `%…%` in the
assign reader (8df240b2f2), the mark switched on `From` (pattern 3, tracked), `Take` eager (pattern 11, tracked). The three
moved golden picks are right.

**Details:** `review.md`. Bench skipped (build-time listing). Decider fixtures green.
