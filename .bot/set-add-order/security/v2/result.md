# set-add-order — security v2 (74bf815d1)

Verdict: PASS. F1 re-rated Low, accepted risk (architect's ruling B).

F1 (a settled variable.set divides variable's share out of `Part`, so file.delete / code.remove / identity.archive go Certain at a 0.43–0.58 module share) stays as v1 describes. The builder's 21 runs on the final binary (`.bot/set-add-order/builder/v1/f1-window.md`) never reach it: the closest, `remove %x%, write to %y%`, gave file 0.90 and variable.set 0.84–0.86, so nothing settled; `delete`, `remove %path%`, `delete the file` gave file 0.98–1.00; a list remove gave list 0.99. The window needs variable.set ≥ 0.9 on a remove, which the decider as it answers today does not give.

Pin: `ModulePartSecurityProbeTests` (9 green) asserts today's edge; each W test flips to Possible when backlog item A lands (the settled share is removed only when the settled action is ≥ 0.9 on its own probability in stage 2's action choice; a mixed choice falls back to the plain share). Re-run W1–W3 plus a split set/remove probe then.

Backlog (under security's name): A, as above.
