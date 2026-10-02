# Issue 40 — re-measure after fix (coder 85e8d27d2)

Binary rebuilt clean from head (f7b8d7377). Properties.goal built **in place**
(`os/system/builder/BuildGoal/Properties.goal`), 10 fresh builds,
`--build={"cache":"skip"}`, step 4 (`call /system/builder/EmitBuildEvent … goal=%goal%`)
counted with jq (`.step[]|select(.index==4)|.code[]|select(.module=="goal" and .name=="call")`).

| Measure | Before fix (77984deec) | After fix (85e8d27d2) |
|---|---|---|
| step 4 goal.call LISTED | 4 / 10 | **10 / 10** |
| goal.call MISSING (saved w/o it) | 0 / 10 | 0 / 10 |
| NO-SAVE (validator rejected, no actions) | 6 / 10 | **0 / 10** |
| Guard: `write out "hi"` → single `output.write`, no alternatives (Certain) | — | **holds** |

`.pr` artifact: `os/system/builder/BuildGoal/.build/properties.pr` — only change vs
committed is the recorded decider confidence for goal.call rising **0.55 → 0.98**
(stage-2's stronger answer now carries); the mapped actions are byte-identical.

Saved per-build `.pr`s: scratchpad `m40_fix_prs/properties_1..10.pr`.

Conclusion: issue 40 fixed. A weak stage-1 common score no longer masks stage-2's
stronger confirmation; a common action certain on its own stage-1 score still stands
(guard).
